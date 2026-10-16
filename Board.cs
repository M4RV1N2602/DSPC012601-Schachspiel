namespace ChessAvalonia;

internal enum PieceType
{
    King,
    Queen,
    Rook,
    Bishop,
    Knight,
    Pawn
}

/// <summary>A chess piece and the side it belongs to.</summary>
internal readonly record struct Piece(bool IsWhite, PieceType Type);

/// <summary>
/// Stores a standard chess position and checks moves against the rules.
/// Squares are numbered from 0 to 63, starting at a8 and ending at h1.
/// </summary>
internal sealed class Board
{
    private const int BoardSize = 8;
    private const int SquareCount = BoardSize * BoardSize;

    private readonly Piece?[] _squares = new Piece?[SquareCount];
    private bool _whiteCanCastleKingSide;
    private bool _whiteCanCastleQueenSide;
    private bool _blackCanCastleKingSide;
    private bool _blackCanCastleQueenSide;
    private int? _enPassantSquare;

    public bool WhiteToMove { get; private set; }
    public List<string> History { get; } = [];

    // The UI uses these accessors to draw the position and ask for legal destinations.
    public Piece?[] Cells => _squares;
    public bool IsCheck => IsKingInCheck(WhiteToMove);

    public Board() => Reset();

    public void Reset()
    {
        Array.Clear(_squares);
        History.Clear();
        WhiteToMove = true;
        _enPassantSquare = null;
        _whiteCanCastleKingSide = true;
        _whiteCanCastleQueenSide = true;
        _blackCanCastleKingSide = true;
        _blackCanCastleQueenSide = true;

        PieceType[] backRank =
        [
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        ];
        for (int file = 0; file < BoardSize; file++)
        {
            _squares[file] = new Piece(false, backRank[file]);       // Black back rank (rank 8)
            _squares[BoardSize + file] = new Piece(false, PieceType.Pawn);       // Black pawns (rank 7)
            _squares[6 * BoardSize + file] = new Piece(true, PieceType.Pawn);    // White pawns (rank 2)
            _squares[7 * BoardSize + file] = new Piece(true, backRank[file]); // White back rank (rank 1)
        }
    }

    /// <summary>Returns all legal destination squares for a piece on <paramref name="fromSquare"/>.</summary>
    public IReadOnlyList<int> GetLegalMoves(int fromSquare) => FindLegalMoves(fromSquare);

    /// <summary>Applies a legal move, records it, and returns false when it is not allowed.</summary>
    public bool TryMove(int fromSquare, int toSquare)
    {
        if (GameOver || !FindLegalMoves(fromSquare).Contains(toSquare))
            return false;

        Piece piece = _squares[fromSquare]!.Value;
        string move = SquareName(fromSquare) + SquareName(toSquare);

        // This simple game automatically promotes a pawn to a queen.
        if (piece.Type == PieceType.Pawn && IsPromotionSquare(toSquare))
            move += "q";

        ApplyMove(fromSquare, toSquare);
        History.Add(move);
        return true;
    }

    /// <summary>True when the side to move has no legal moves (checkmate or stalemate).</summary>
    public bool GameOver
    {
        get
        {
            for (int square = 0; square < SquareCount; square++)
            {
                if (_squares[square] is Piece piece && piece.IsWhite == WhiteToMove && FindLegalMoves(square).Count > 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Restarts the game and replays a saved sequence of coordinate moves.</summary>
    public void Load(IEnumerable<string> moves)
    {
        string[] savedMoves = moves.ToArray();
        Reset();

        foreach (string move in savedMoves)
        {
            if (move.Length < 4)
                throw new InvalidDataException("Ungültiger Zug.");

            int fromSquare = ParseSquare(move[..2]);
            int toSquare = ParseSquare(move.Substring(2, 2));
            if (!FindLegalMoves(fromSquare).Contains(toSquare))
                throw new InvalidDataException($"Der Zug {move} ist in dieser Stellung nicht zulässig.");

            ApplyMove(fromSquare, toSquare);
            History.Add(move);
        }
    }

    /// <summary>
    /// Candidate moves follow how each piece moves. This method does not yet check
    /// whether moving the piece would leave its own king in check.
    /// </summary>
    private List<int> FindLegalMoves(int fromSquare)
    {
        if (fromSquare < 0 || fromSquare >= SquareCount || _squares[fromSquare] is not Piece piece || piece.IsWhite != WhiteToMove)
            return [];

        var legalMoves = new List<int>();
        foreach (int toSquare in FindCandidateMoves(fromSquare, piece))
        {
            PositionSnapshot beforeMove = CapturePosition();
            ApplyMove(fromSquare, toSquare);
            bool leavesKingSafe = !IsKingInCheck(piece.IsWhite);
            RestorePosition(beforeMove);

            if (leavesKingSafe)
                legalMoves.Add(toSquare);
        }

        return legalMoves;
    }

    private IEnumerable<int> FindCandidateMoves(int fromSquare, Piece piece)
    {
        return piece.Type switch
        {
            PieceType.Pawn => FindPawnMoves(fromSquare, piece),
            PieceType.Knight => FindKnightMoves(fromSquare, piece),
            PieceType.King => FindKingMoves(fromSquare, piece),
            PieceType.Bishop => FindSlidingMoves(fromSquare, piece, BishopDirections),
            PieceType.Rook => FindSlidingMoves(fromSquare, piece, RookDirections),
            _ => FindSlidingMoves(fromSquare, piece, QueenDirections)
        };
    }

    private IEnumerable<int> FindPawnMoves(int fromSquare, Piece pawn)
    {
        int row = Row(fromSquare);
        int file = File(fromSquare);
        int direction = pawn.IsWhite ? -1 : 1;
        int startingRow = pawn.IsWhite ? 6 : 1;
        int oneStepRow = row + direction;

        // Pawns move forward into empty squares. From their starting row they may move two.
        if (IsInsideBoard(oneStepRow, file) && IsEmpty(oneStepRow, file))
        {
            yield return Square(oneStepRow, file);

            int twoStepRow = row + 2 * direction;
            if (row == startingRow && IsEmpty(twoStepRow, file))
                yield return Square(twoStepRow, file);
        }

        // Pawns capture diagonally. En passant is the exception where the target square is empty.
        foreach (int fileOffset in new[] { -1, 1 })
        {
            int targetFile = file + fileOffset;
            if (!IsInsideBoard(oneStepRow, targetFile))
                continue;

            int targetSquare = Square(oneStepRow, targetFile);
            if (IsEnemyPiece(oneStepRow, targetFile, pawn) || _enPassantSquare == targetSquare)
                yield return targetSquare;
        }
    }

    private IEnumerable<int> FindKnightMoves(int fromSquare, Piece knight)
    {
        int row = Row(fromSquare);
        int file = File(fromSquare);
        int[,] offsets = { { -2, -1 }, { -2, 1 }, { -1, -2 }, { -1, 2 }, { 1, -2 }, { 1, 2 }, { 2, -1 }, { 2, 1 } };

        for (int index = 0; index < offsets.GetLength(0); index++)
        {
            int targetRow = row + offsets[index, 0];
            int targetFile = file + offsets[index, 1];
            if (CanMoveTo(targetRow, targetFile, knight))
                yield return Square(targetRow, targetFile);
        }
    }

    private IEnumerable<int> FindKingMoves(int fromSquare, Piece king)
    {
        int row = Row(fromSquare);
        int file = File(fromSquare);

        // A king can move one square in any direction.
        for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
        for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
        {
            if (rowOffset == 0 && fileOffset == 0)
                continue;

            int targetRow = row + rowOffset;
            int targetFile = file + fileOffset;
            if (CanMoveTo(targetRow, targetFile, king))
                yield return Square(targetRow, targetFile);
        }

        foreach (int castleDestination in FindCastlingMoves(fromSquare, king))
            yield return castleDestination;
    }

    private IEnumerable<int> FindCastlingMoves(int kingSquare, Piece king)
    {
        int homeSquare = king.IsWhite ? 60 : 4;
        if (kingSquare != homeSquare || IsKingInCheck(king.IsWhite))
            yield break;

        bool canCastleKingSide = king.IsWhite ? _whiteCanCastleKingSide : _blackCanCastleKingSide;
        bool canCastleQueenSide = king.IsWhite ? _whiteCanCastleQueenSide : _blackCanCastleQueenSide;
        bool enemyIsWhite = !king.IsWhite;

        // King side: the king moves two squares toward the rook on the h-file.
        if (canCastleKingSide && IsRookAt(homeSquare + 3, king.IsWhite)
            && _squares[homeSquare + 1] is null && _squares[homeSquare + 2] is null
            && !IsSquareAttacked(homeSquare + 1, enemyIsWhite)
            && !IsSquareAttacked(homeSquare + 2, enemyIsWhite))
        {
            yield return homeSquare + 2;
        }

        // Queen side: the b-file must be empty too, even though the king passes d-file.
        if (canCastleQueenSide && IsRookAt(homeSquare - 4, king.IsWhite)
            && _squares[homeSquare - 1] is null && _squares[homeSquare - 2] is null && _squares[homeSquare - 3] is null
            && !IsSquareAttacked(homeSquare - 1, enemyIsWhite)
            && !IsSquareAttacked(homeSquare - 2, enemyIsWhite))
        {
            yield return homeSquare - 2;
        }
    }

    private IEnumerable<int> FindSlidingMoves(int fromSquare, Piece piece, (int Row, int File)[] directions)
    {
        int startRow = Row(fromSquare);
        int startFile = File(fromSquare);

        foreach ((int rowStep, int fileStep) in directions)
        {
            int row = startRow + rowStep;
            int file = startFile + fileStep;

            while (IsInsideBoard(row, file))
            {
                if (_squares[Square(row, file)] is null)
                {
                    yield return Square(row, file);
                }
                else
                {
                    if (IsEnemyPiece(row, file, piece))
                        yield return Square(row, file);

                    // Pieces cannot jump over another piece, whether friend or foe.
                    break;
                }

                row += rowStep;
                file += fileStep;
            }
        }
    }

    private void ApplyMove(int fromSquare, int toSquare)
    {
        Piece movingPiece = _squares[fromSquare]!.Value;
        Piece? capturedPiece = _squares[toSquare];

        // En passant captures the pawn beside the destination, not on the destination.
        if (movingPiece.Type == PieceType.Pawn && toSquare == _enPassantSquare && capturedPiece is null)
            _squares[toSquare + (movingPiece.IsWhite ? 8 : -8)] = null;

        // In castling the rook jumps over the king to the square next to it.
        if (movingPiece.Type == PieceType.King && Math.Abs(toSquare - fromSquare) == 2)
        {
            bool kingSide = toSquare > fromSquare;
            int rookFrom = kingSide ? fromSquare + 3 : fromSquare - 4;
            int rookTo = kingSide ? fromSquare + 1 : fromSquare - 1;
            _squares[rookTo] = _squares[rookFrom];
            _squares[rookFrom] = null;
        }

        if (capturedPiece is Piece capturedRook && capturedRook.Type == PieceType.Rook)
            RemoveCastlingRightForRookAt(toSquare);

        // This version automatically turns a pawn reaching the last rank into a queen.
        _squares[toSquare] = movingPiece.Type == PieceType.Pawn && IsPromotionSquare(toSquare)
            ? new Piece(movingPiece.IsWhite, PieceType.Queen)
            : movingPiece;
        _squares[fromSquare] = null;

        if (movingPiece.Type == PieceType.King)
            RemoveCastlingRightsForKing(movingPiece.IsWhite);
        else if (movingPiece.Type == PieceType.Rook)
            RemoveCastlingRightForRookAt(fromSquare);

        // En passant is available for one turn only, after a pawn advances two squares.
        _enPassantSquare = movingPiece.Type == PieceType.Pawn && Math.Abs(toSquare - fromSquare) == 2 * BoardSize
            ? (fromSquare + toSquare) / 2
            : null;

        WhiteToMove = !WhiteToMove;
    }

    private bool IsKingInCheck(bool isWhite)
    {
        int kingSquare = Array.FindIndex(_squares,
            piece => piece is Piece chessPiece && chessPiece.Type == PieceType.King && chessPiece.IsWhite == isWhite);

        // A legal game always has both kings. Treat a missing king as unsafe when loading bad data.
        return kingSquare < 0 || IsSquareAttacked(kingSquare, !isWhite);
    }

    private bool IsSquareAttacked(int targetSquare, bool byWhite)
    {
        int targetRow = Row(targetSquare);
        int targetFile = File(targetSquare);

        for (int fromSquare = 0; fromSquare < SquareCount; fromSquare++)
        {
            if (_squares[fromSquare] is not Piece attacker || attacker.IsWhite != byWhite)
                continue;

            int rowDistance = targetRow - Row(fromSquare);
            int fileDistance = targetFile - File(fromSquare);
            int absoluteRowDistance = Math.Abs(rowDistance);
            int absoluteFileDistance = Math.Abs(fileDistance);

            if (attacker.Type == PieceType.Pawn && rowDistance == (byWhite ? -1 : 1) && absoluteFileDistance == 1)
                return true;
            if (attacker.Type == PieceType.Knight && absoluteRowDistance * absoluteFileDistance == 2)
                return true;
            if (attacker.Type == PieceType.King && Math.Max(absoluteRowDistance, absoluteFileDistance) == 1)
                return true;

            if (attacker.Type is PieceType.Bishop or PieceType.Rook or PieceType.Queen
                && IsSlidingPieceAttacking(fromSquare, targetSquare, attacker.Type, rowDistance, fileDistance))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSlidingPieceAttacking(int fromSquare, int targetSquare, PieceType type, int rowDistance, int fileDistance)
    {
        bool isDiagonal = Math.Abs(rowDistance) == Math.Abs(fileDistance);
        bool isStraight = rowDistance == 0 || fileDistance == 0;

        if (type == PieceType.Bishop && !isDiagonal
            || type == PieceType.Rook && !isStraight
            || type == PieceType.Queen && !isDiagonal && !isStraight)
            return false;

        int rowStep = Math.Sign(rowDistance);
        int fileStep = Math.Sign(fileDistance);
        int row = Row(fromSquare) + rowStep;
        int file = File(fromSquare) + fileStep;

        // A sliding piece attacks the target only if no piece blocks the line.
        while (Square(row, file) != targetSquare)
        {
            if (_squares[Square(row, file)] is not null)
                return false;

            row += rowStep;
            file += fileStep;
        }

        return true;
    }

    private bool CanMoveTo(int row, int file, Piece movingPiece) =>
        IsInsideBoard(row, file) && (_squares[Square(row, file)] is null || IsEnemyPiece(row, file, movingPiece));

    private bool IsEnemyPiece(int row, int file, Piece movingPiece) =>
        _squares[Square(row, file)] is Piece otherPiece
        && otherPiece.IsWhite != movingPiece.IsWhite
        && otherPiece.Type != PieceType.King; // Kings are checked, never captured.

    private bool IsEmpty(int row, int file) => _squares[Square(row, file)] is null;
    private bool IsRookAt(int square, bool isWhite) => _squares[square] is Piece rook && rook.Type == PieceType.Rook && rook.IsWhite == isWhite;
    private static bool IsInsideBoard(int row, int file) => row >= 0 && row < BoardSize && file >= 0 && file < BoardSize;
    private static bool IsPromotionSquare(int square) => Row(square) is 0 or 7;
    private static int Row(int square) => square / BoardSize;
    private static int File(int square) => square % BoardSize;
    private static int Square(int row, int file) => row * BoardSize + file;
    private static string SquareName(int square) => $"{(char)('a' + File(square))}{8 - Row(square)}";

    private static int ParseSquare(string name)
    {
        int file = char.ToLowerInvariant(name[0]) - 'a';
        int rank = name[1] - '0';
        if (file is < 0 or >= BoardSize || rank is < 1 or > 8)
            throw new InvalidDataException($"Ungültiges Feld: {name}");
        return Square(8 - rank, file);
    }

    private void RemoveCastlingRightsForKing(bool isWhite)
    {
        if (isWhite)
            _whiteCanCastleKingSide = _whiteCanCastleQueenSide = false;
        else
            _blackCanCastleKingSide = _blackCanCastleQueenSide = false;
    }

    private void RemoveCastlingRightForRookAt(int square)
    {
        switch (square)
        {
            case 63: _whiteCanCastleKingSide = false; break; // h1
            case 56: _whiteCanCastleQueenSide = false; break; // a1
            case 7: _blackCanCastleKingSide = false; break;  // h8
            case 0: _blackCanCastleQueenSide = false; break; // a8
        }
    }

    private PositionSnapshot CapturePosition() => new(
        (Piece?[])_squares.Clone(), WhiteToMove, _enPassantSquare,
        _whiteCanCastleKingSide, _whiteCanCastleQueenSide,
        _blackCanCastleKingSide, _blackCanCastleQueenSide);

    private void RestorePosition(PositionSnapshot position)
    {
        Array.Copy(position.Squares, _squares, SquareCount);
        WhiteToMove = position.WhiteToMove;
        _enPassantSquare = position.EnPassantSquare;
        _whiteCanCastleKingSide = position.WhiteCanCastleKingSide;
        _whiteCanCastleQueenSide = position.WhiteCanCastleQueenSide;
        _blackCanCastleKingSide = position.BlackCanCastleKingSide;
        _blackCanCastleQueenSide = position.BlackCanCastleQueenSide;
    }

    private sealed record PositionSnapshot(
        Piece?[] Squares,
        bool WhiteToMove,
        int? EnPassantSquare,
        bool WhiteCanCastleKingSide,
        bool WhiteCanCastleQueenSide,
        bool BlackCanCastleKingSide,
        bool BlackCanCastleQueenSide);

    private static readonly (int Row, int File)[] BishopDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1) };
    private static readonly (int Row, int File)[] RookDirections =
        { (-1, 0), (1, 0), (0, -1), (0, 1) };
    private static readonly (int Row, int File)[] QueenDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1) };
}
