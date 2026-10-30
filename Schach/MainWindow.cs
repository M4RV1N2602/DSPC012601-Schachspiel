using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Schach;

public sealed class MainWindow : Window
{
    private readonly Board _game = new();
    private readonly Border[,] _squares = new Border[8, 8];
    private readonly TextBlock _status = new() { FontSize = 18, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _moves = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14 };
    private int? _selected;

    public MainWindow()
    {
        Title = "Schach – zwei Personen, ein Gerät";
        Width = 880; Height = 720; MinWidth = 700; MinHeight = 600;
        var root = new Grid { Margin = new Thickness(24), ColumnDefinitions = new ColumnDefinitions("*,280") };
        var board = new Grid { Width = 560, Height = 560, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 8; i++) { board.RowDefinitions.Add(new RowDefinition(GridLength.Star)); board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); }
        for (int r = 0; r < 8; r++) for (int c = 0; c < 8; c++)
        {
            int square = r * 8 + c;
            var cell = new Border
            {
                Background = new SolidColorBrush(Color.Parse(((r + c) % 2 == 0) ? "#F0D9B5" : "#B58863")),
                Child = new TextBlock { FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            _squares[r, c] = cell; Grid.SetRow(cell, r); Grid.SetColumn(cell, c); cell.PointerPressed += (_, _) => ClickSquare(square); board.Children.Add(cell);
        }
        var side = new StackPanel { Margin = new Thickness(24, 0, 0, 0), Spacing = 14 };
        side.Children.Add(_status);
        side.Children.Add(new TextBlock { Text = "Zugliste", FontSize = 16, FontWeight = FontWeight.Bold });
        side.Children.Add(new ScrollViewer { Height = 420, Content = _moves });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var fresh = new Button { Content = "Neues Spiel" }; fresh.Click += (_, _) => { _game.Reset(); _selected = null; Refresh(); };
        var save = new Button { Content = "Speichern" }; save.Click += async (_, _) => await SaveGame();
        var load = new Button { Content = "Laden" }; load.Click += async (_, _) => await LoadGame();
        actions.Children.Add(fresh); actions.Children.Add(save); actions.Children.Add(load); side.Children.Add(actions);
        Grid.SetColumn(board, 0); Grid.SetColumn(side, 1); root.Children.Add(board); root.Children.Add(side); Content = root;
        Refresh();
    }

    private void ClickSquare(int sq)
    {
        if (_selected is null) { if (_game.Cells[sq] is not null && _game.Cells[sq]!.Value.IsWhite == _game.WhiteToMove) _selected = sq; }
        else
        {
            if (_game.TryMove(_selected.Value, sq)) _selected = null;
            else if (_game.Cells[sq] is not null && _game.Cells[sq]!.Value.IsWhite == _game.WhiteToMove) _selected = sq;
            else _selected = null;
        }
        Refresh();
    }

    private void Refresh()
    {
        HashSet<int> destinations = _selected is int selected
            ? _game.GetLegalMoves(selected).ToHashSet()
            : new HashSet<int>();
        for (int r = 0; r < 8; r++) for (int c = 0; c < 8; c++)
        {
            int i = r * 8 + c; var p = _game.Cells[i]; var cell = _squares[r, c]; var glyph = (TextBlock)cell.Child!;
            glyph.Text = p is null ? "" : PieceGlyph(p.Value);
            glyph.Foreground = p is Piece piece && piece.IsWhite ? Brushes.White : Brushes.Black;
            var baseColor = ((r + c) % 2 == 0) ? "#F0D9B5" : "#B58863";
            string color = _selected == i ? "#A9C98B" : destinations.Contains(i) ? "#8FCB82" : baseColor;
            cell.Background = new SolidColorBrush(Color.Parse(color));
            cell.BorderBrush = destinations.Contains(i) ? new SolidColorBrush(Color.Parse("#356B35")) : Brushes.Transparent;
            cell.BorderThickness = destinations.Contains(i) ? new Thickness(3) : new Thickness(0);
        }
        _status.Text = _game.GameOver ? (_game.IsCheck ? "Schachmatt – " : "Patt – ") + (_game.WhiteToMove ? "Schwarz gewinnt" : "Weiß gewinnt") : (_game.WhiteToMove ? "Weiß am Zug" : "Schwarz am Zug") + (_game.IsCheck ? " · Schach" : "");
        _moves.Text = string.Join("\n", _game.History);
    }
    private static string PieceGlyph(Piece p) => (p.IsWhite, p.Type) switch
    {
        (true, PieceType.King) => "♔", (true, PieceType.Queen) => "♕", (true, PieceType.Rook) => "♖",
        (true, PieceType.Bishop) => "♗", (true, PieceType.Knight) => "♘", (true, PieceType.Pawn) => "♙",
        (false, PieceType.King) => "♚", (false, PieceType.Queen) => "♛", (false, PieceType.Rook) => "♜",
        (false, PieceType.Bishop) => "♝", (false, PieceType.Knight) => "♞", _ => "♟"
    };
    private async Task SaveGame()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Partie speichern",
            SuggestedFileName = "schachpartie.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Schachpartie") { Patterns = ["*.json"] }]
        });
        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        await JsonSerializer.SerializeAsync(stream, _game.History);
    }

    private async Task LoadGame()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Partie laden",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Schachpartie") { Patterns = ["*.json"] }]
        });
        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            _game.Load(await JsonSerializer.DeserializeAsync<List<string>>(stream) ?? []);
            _selected = null;
            Refresh();
        }
        catch (Exception e) { await new Window { Title = "Fehler", Width = 400, Height = 120, Content = new TextBlock { Text = $"Partie konnte nicht geladen werden: {e.Message}", Margin = new Thickness(16) } }.ShowDialog(this); }
    }
}
