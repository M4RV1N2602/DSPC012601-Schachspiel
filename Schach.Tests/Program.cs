using System.Reflection;
using System.Text.Json;
using Schach;

var tests = new (string Name, Action Run)[]
{
    ("Startposition und legale Anfangszüge", StartPosition),
    ("Illegale Züge und Spielerwechsel", IllegalMovesAndTurns),
    ("Eigener König bleibt geschützt", KingSafety),
    ("Schach und Schachmatt", Checkmate),
    ("Patt", Stalemate),
    ("Rochade", Castling),
    ("En passant", EnPassant),
    ("Automatische Umwandlung", Promotion),
    ("JSON-Zugliste speichern und Partie laden", SaveAndLoad),
    ("Ungültige gespeicherte Züge ablehnen", InvalidLoad)
};

int failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS  {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL  {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"Ergebnis: {tests.Length - failed}/{tests.Length} bestanden, {failed} fehlgeschlagen.");
return failed == 0 ? 0 : 1;

static void StartPosition()
{
    var b = new Board();
    Equal(true, b.WhiteToMove, "Weiß beginnt");
    Equal(false, b.GameOver, "Spiel nicht beendet");
    SetEqual([40, 32], b.GetLegalMoves(48), "Bauer a2");
    SetEqual([44, 36], b.GetLegalMoves(52), "Bauer e2");
    SetEqual([40, 42], b.GetLegalMoves(57), "Springer b1");
}

static void IllegalMovesAndTurns()
{
    var b = new Board();
    Equal(false, b.TryMove(48, 41), "Bauer darf nicht diagonal ins Leere ziehen");
    Equal(false, b.TryMove(8, 16), "Schwarz ist noch nicht am Zug");
    Equal(0, b.History.Count, "Ungültige Züge werden nicht gespeichert");
    Equal(true, b.TryMove(52, 36), "e2-e4");
    Equal(false, b.WhiteToMove, "Danach ist Schwarz am Zug");
    Equal(true, b.TryMove(12, 28), "e7-e5");
    Equal(true, b.WhiteToMove, "Danach ist Weiß am Zug");
}

static void KingSafety()
{
    var b = Position(whiteToMove: true,
        ("e1", true, PieceType.King), ("e2", true, PieceType.Rook), ("a8", false, PieceType.King), ("e8", false, PieceType.Rook));
    Equal(false, b.GetLegalMoves(Sq("e2")).Contains(Sq("f2")), "Fesselung darf die e-Linie nicht öffnen");
}

static void Checkmate()
{
    var b = new Board();
    Play(b, "f2f3", "e7e5", "g2g4", "d8h4");
    Equal(true, b.IsCheck, "Weiß steht im Schach");
    Equal(true, b.GameOver, "Matt beendet die Partie");
    Equal(false, b.TryMove(Sq("e2"), Sq("e4")), "Nach Spielende kein Zug");
}

static void Stalemate()
{
    var b = Position(whiteToMove: false,
        ("f7", true, PieceType.King), ("g6", true, PieceType.Queen), ("h8", false, PieceType.King));
    Equal(false, b.IsCheck, "Schwarzer König steht nicht im Schach");
    Equal(true, b.GameOver, "Schwarz hat keinen legalen Zug");
}

static void Castling()
{
    var b = Position(whiteToMove: true,
        ("e1", true, PieceType.King), ("h1", true, PieceType.Rook), ("e8", false, PieceType.King));
    Equal(true, b.GetLegalMoves(Sq("e1")).Contains(Sq("g1")), "Kurze Rochade verfügbar");
    Equal(true, b.TryMove(Sq("e1"), Sq("g1")), "Kurze Rochade ausführen");
    Equal(PieceType.King, b.Cells[Sq("g1")]!.Value.Type, "König auf g1");
    Equal(PieceType.Rook, b.Cells[Sq("f1")]!.Value.Type, "Turm auf f1");
}

static void EnPassant()
{
    var b = new Board();
    Play(b, "e2e4", "a7a6", "e4e5", "d7d5");
    Equal(true, b.GetLegalMoves(Sq("e5")).Contains(Sq("d6")), "En passant verfügbar");
    Equal(true, b.TryMove(Sq("e5"), Sq("d6")), "En passant ausführen");
    Equal(null, b.Cells[Sq("d5")], "Geschlagener Bauer entfernt");
    Equal(PieceType.Pawn, b.Cells[Sq("d6")]!.Value.Type, "Weißer Bauer auf d6");
}

static void Promotion()
{
    var b = Position(whiteToMove: true,
        ("a7", true, PieceType.Pawn), ("h1", true, PieceType.King), ("h8", false, PieceType.King));
    Equal(true, b.TryMove(Sq("a7"), Sq("a8")), "Bauer erreicht letzte Reihe");
    Equal(PieceType.Queen, b.Cells[Sq("a8")]!.Value.Type, "Umwandlung zur Dame");
    Equal("a7a8q", b.History.Single(), "Umwandlung im Zugverlauf");
}

static void SaveAndLoad()
{
    var original = new Board();
    Play(original, "e2e4", "e7e5", "g1f3", "b8c6", "f1b5");
    string json = JsonSerializer.Serialize(original.History);
    var moves = JsonSerializer.Deserialize<List<string>>(json)!;
    var restored = new Board();
    restored.Load(moves);
    Equal(json, JsonSerializer.Serialize(restored.History), "Zugliste nach JSON-Roundtrip");
    Equal(original.WhiteToMove, restored.WhiteToMove, "Am Zug nach Laden");
    for (int i = 0; i < 64; i++) Equal(original.Cells[i], restored.Cells[i], $"Feld {i} nach Laden");
}

static void InvalidLoad()
{
    var b = new Board();
    Throws<InvalidDataException>(() => b.Load(["e2e5"]), "Unmöglichen Zug ablehnen");
}

static Board Position(bool whiteToMove, params (string Square, bool White, PieceType Type)[] pieces)
{
    var b = new Board();
    Array.Clear(b.Cells);
    foreach (var (square, white, type) in pieces) b.Cells[Sq(square)] = new Piece(white, type);
    typeof(Board).GetProperty(nameof(Board.WhiteToMove))!.SetValue(b, whiteToMove);
    return b;
}

static void Play(Board b, params string[] moves)
{
    foreach (string move in moves)
        Equal(true, b.TryMove(Sq(move[..2]), Sq(move[2..4])), $"Zug {move}");
}
static int Sq(string name) => (8 - (name[1] - '0')) * 8 + (char.ToLowerInvariant(name[0]) - 'a');
static void Equal<T>(T expected, T actual, string context)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{context}: erwartet '{expected}', erhalten '{actual}'.");
}
static void SetEqual(IEnumerable<int> expected, IEnumerable<int> actual, string context)
{
    if (!expected.Order().SequenceEqual(actual.Order()))
        throw new InvalidOperationException($"{context}: erwartet [{string.Join(',', expected)}], erhalten [{string.Join(',', actual)}].");
}
static void Throws<T>(Action action, string context) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"{context}: Ausnahme {typeof(T).Name} erwartet.");
}


