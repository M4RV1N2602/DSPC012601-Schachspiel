using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Schach.Views;

public partial class MainView : UserControl
{
    private readonly Board _game = new();
    private readonly Border[,] _squares = new Border[8, 8];
    private int? _selected;

    public MainView()
    {
        InitializeComponent();

        for (int i = 0; i < 8; i++)
        {
            Board.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            int square = r * 8 + c;
            var cell = new Border
            {
                Background = new SolidColorBrush(Color.Parse(((r + c) % 2 == 0) ? "#F0D9B5" : "#B58863")),
                Child = new TextBlock
                {
                    FontSize = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _squares[r, c] = cell;
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            cell.PointerPressed += (_, _) => ClickSquare(square);
            Board.Children.Add(cell);
        }

        Refresh();
    }

    private void NewGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _game.Reset();
        _selected = null;
        Refresh();
    }

    private async void SaveGameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await SaveGame();

    private async void LoadGameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await LoadGame();

    private void ClickSquare(int sq)
    {
        if (_selected is null)
        {
            if (_game.Cells[sq] is not null && _game.Cells[sq]!.Value.IsWhite == _game.WhiteToMove)
                _selected = sq;
        }
        else
        {
            if (_game.TryMove(_selected.Value, sq))
                _selected = null;
            else if (_game.Cells[sq] is not null && _game.Cells[sq]!.Value.IsWhite == _game.WhiteToMove)
                _selected = sq;
            else
                _selected = null;
        }

        Refresh();
    }

    private void Refresh()
    {
        HashSet<int> destinations = _selected is int selected
            ? _game.GetLegalMoves(selected).ToHashSet()
            : new HashSet<int>();

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            int i = r * 8 + c;
            var piece = _game.Cells[i];
            var cell = _squares[r, c];
            var glyph = (TextBlock)cell.Child!;
            glyph.Text = piece is null ? "" : PieceGlyph(piece.Value);
            glyph.Foreground = piece is Piece value && value.IsWhite ? Brushes.White : Brushes.Black;
            var baseColor = ((r + c) % 2 == 0) ? "#F0D9B5" : "#B58863";
            string color = _selected == i ? "#A9C98B" : destinations.Contains(i) ? "#8FCB82" : baseColor;
            cell.Background = new SolidColorBrush(Color.Parse(color));
            cell.BorderBrush = destinations.Contains(i)
                ? new SolidColorBrush(Color.Parse("#356B35"))
                : Brushes.Transparent;
            cell.BorderThickness = destinations.Contains(i) ? new Thickness(3) : new Thickness(0);
        }

        Status.Text = _game.GameOver
            ? (_game.IsCheck ? "Schachmatt – " : "Patt – ") + (_game.WhiteToMove ? "Schwarz gewinnt" : "Weiß gewinnt")
            : (_game.WhiteToMove ? "Weiß am Zug" : "Schwarz am Zug") + (_game.IsCheck ? " · Schach" : "");
        Moves.Text = string.Join("\n", _game.History);
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
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
        catch (Exception e)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is not null)
            {
                await new Window
                {
                    Title = "Fehler",
                    Width = 400,
                    Height = 120,
                    Content = new TextBlock
                    {
                        Text = $"Partie konnte nicht geladen werden: {e.Message}",
                        Margin = new Thickness(16)
                    }
                }.ShowDialog(owner);
            }
        }
    }
}
