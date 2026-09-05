using Match3Lab.Core;
using Xunit;

namespace Match3Lab.Core.Tests
{
    public class BoardTests
    {
        [Fact]
        public void Indexer_returns_a_reference_so_writes_stick()
        {
            var board = new Board(3, 2);
            board[1, 1].Piece = Piece.Normal(4);
            board[1, 1].Grass = 2;

            Assert.Equal(Piece.Normal(4), board[new GridPos(1, 1)].Piece);
            Assert.Equal(2, board[1, 1].Grass);
            Assert.True(board[0, 0].Piece.IsEmpty);
        }

        [Fact]
        public void Clone_is_independent()
        {
            var board = new Board(2, 2);
            board[0, 0].Piece = Piece.Normal(1);
            var copy = board.Clone();
            copy[0, 0].Piece = Piece.Special(PieceType.Bomb);

            Assert.Equal(Piece.Normal(1), board[0, 0].Piece);
            Assert.Equal(PieceType.Bomb, copy[0, 0].Piece.Type);
        }

        [Fact]
        public void Dump_uses_the_level_text_alphabet()
        {
            var board = new Board(3, 1);
            board[0, 0] = Cell.MakeHole();
            board[1, 0].Piece = Piece.Normal(2);
            board[1, 0].Ice = 1;
            board[2, 0] = Cell.MakeBox(2);

            Assert.Equal("# 2i B\n", board.Dump());
        }

        [Fact]
        public void GridPos_adjacency_is_edge_only()
        {
            var p = new GridPos(2, 2);
            Assert.True(p.IsAdjacentTo(new GridPos(2, 3)));
            Assert.True(p.IsAdjacentTo(new GridPos(1, 2)));
            Assert.False(p.IsAdjacentTo(new GridPos(3, 3)));
            Assert.False(p.IsAdjacentTo(p));
        }
    }
}
