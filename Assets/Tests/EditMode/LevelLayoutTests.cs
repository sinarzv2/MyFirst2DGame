using System;
using System.IO;
using System.Linq;
using Level;
using LevelTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// LevelLayout alone (the text map of a level, and the ground tile each cell needs), and the files the
// Tools > Level menu works with. No scene needed.
namespace Tests.EditMode
{
    public class LevelLayoutTests
    {
        // Block A (2 wide, 2 tall), block B right beside it and a cell higher, a gap, block C.
        private const string Rows = "..BB...\nAABB.CC\nAABB.CC\n";
        private const string Example = "// a comment line\norigin 10 -3\n" + Rows;

        private static GroundPiece Piece(GroundRow row, GroundColumn column) => new GroundPiece(row, column);

        [Test]
        public void Parse_ReadsTheOrigin_AndTheRowsTopFirst()
        {
            var layout = LevelLayout.Parse(Example);
            Assert.AreEqual(new Vector2Int(10, -3), layout.Origin, "origin = the bottom-left character's cell");
            Assert.AreEqual(7, layout.Width);
            Assert.AreEqual(3, layout.Height);
            Assert.AreEqual('A', layout.At(10, -3), "bottom-left");
            Assert.AreEqual('B', layout.At(12, -1), "top row");
            Assert.AreEqual(LevelLayout.Empty, layout.At(14, -2), "the gap");
            Assert.AreEqual(LevelLayout.Empty, layout.At(9, -3), "outside the map is empty");
        }

        [Test]
        public void Pieces_FollowTheBlock_EachWithItsOwnOutline()
        {
            var pieces = LevelLayout.Parse(Example).Pieces();
            Assert.AreEqual(14, pieces.Count, "one piece per ground cell");
            Assert.AreEqual(Piece(GroundRow.Top, GroundColumn.Left), pieces[new Vector2Int(10, -2)], "A: grass, left end");
            Assert.AreEqual(Piece(GroundRow.Top, GroundColumn.Right), pieces[new Vector2Int(11, -2)], "A: grass, right end (B beside it is another block)");
            Assert.AreEqual(Piece(GroundRow.Bottom, GroundColumn.Left), pieces[new Vector2Int(10, -3)], "A: outlined bottom");
            Assert.AreEqual(Piece(GroundRow.Top, GroundColumn.Left), pieces[new Vector2Int(12, -1)], "B: grass, left end");
            Assert.AreEqual(Piece(GroundRow.Fill, GroundColumn.Left), pieces[new Vector2Int(12, -2)], "B: fill with its own left outline, beside A");
            Assert.AreEqual(Piece(GroundRow.Fill, GroundColumn.Right), pieces[new Vector2Int(13, -2)], "B: fill, right end");
            Assert.AreEqual(Piece(GroundRow.Bottom, GroundColumn.Left), pieces[new Vector2Int(15, -3)], "C: outlined bottom, left end");
        }

        [TestCase("origin 0 0\nAA\n", "tall", TestName = "Layout problem: a block one cell tall")]
        [TestCase("origin 0 0\nA\nA\n", "wide", TestName = "Layout problem: a block one cell wide")]
        public void Problems_NameBlocksNoTileFits(string text, string word)
        {
            var layout = LevelLayout.Parse(text);
            var problems = layout.Problems();
            Assert.That(problems, Is.Not.Empty);
            StringAssert.Contains(word, problems[0]);
            Assert.Throws<InvalidOperationException>(() => layout.Pieces(), "no pieces for a layout with problems");
        }

        [TestCase("AA\nAA\n", "origin", TestName = "Layout text: no origin line")]
        [TestCase("origin 0 0\nAA\nAAA\n", "line 3", TestName = "Layout text: rows of different lengths")]
        [TestCase("origin 0 0\nA A\nA A\n", "line 2", TestName = "Layout text: a space in a row")]
        public void Parse_TellsWhatIsWrong(string text, string expected)
        {
            var error = Assert.Throws<FormatException>(() => LevelLayout.Parse(text));
            StringAssert.Contains(expected, error.Message);
        }

        // Save Layout From Level works this way: tiles -> pieces -> layout. It must give the same tiles back.
        [Test]
        public void FromPieces_GivesBackTheSameLayout()
        {
            var pieces = LevelLayout.Parse(Example).Pieces();
            var again = LevelLayout.FromPieces(pieces);
            CollectionAssert.AreEquivalent(pieces, again.Pieces(), "the same tile in every cell");
            Assert.AreEqual(new Vector2Int(10, -3), again.Origin);
            StringAssert.Contains(Rows, again.ToText().Replace("\r", ""), "blocks lettered A, B, C from the left");
        }

        // A wrong tile painted in the middle of a block (an outlined bottom, mid-column) doesn't
        // change the block: the layout gives back the right tile there. Save Layout From Level warns
        // about such tiles, since saving would replace them on the next build.
        [Test]
        public void FromPieces_OfAMisplacedEdgeTile_GivesTheRightTileThere()
        {
            var pieces = LevelLayout.Parse("origin 0 0\nAA\nAA\nAA\n").Pieces();
            var wrong = new Vector2Int(0, 1);
            pieces[wrong] = Piece(GroundRow.Bottom, GroundColumn.Left);
            var again = LevelLayout.FromPieces(pieces).Pieces();
            Assert.AreEqual(Piece(GroundRow.Fill, GroundColumn.Left), again[wrong], "the tile the block needs there");
            Assert.AreEqual(pieces.Count, again.Count, "the same cells");
        }

        // '$' is a coin: not ground, so the ground under it still gets its grass.
        [Test]
        public void Coins_AreReadApart_FromTheGround()
        {
            var layout = LevelLayout.Parse("origin 0 0\n.$.\nAAA\nAAA\n");
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 2) }, layout.CoinCells());
            var pieces = layout.Pieces();
            Assert.IsFalse(pieces.ContainsKey(new Vector2Int(1, 2)), "a coin is not ground");
            Assert.AreEqual(Piece(GroundRow.Top, GroundColumn.Middle), pieces[new Vector2Int(1, 1)], "grass under the coin");
        }

        [Test]
        public void FromPieces_KeepsTheCoins()
        {
            var layout = LevelLayout.Parse("origin 0 0\n.$.\nAAA\nAAA\n");
            var again = LevelLayout.FromPieces(layout.Pieces(), layout.CoinCells());
            CollectionAssert.AreEqual(layout.CoinCells(), again.CoinCells());
            StringAssert.Contains(".$.\nAAA\nAAA\n", again.ToText());
        }

        // '@' is an enemy: not ground either, and kept by the way back, with the coins.
        [Test]
        public void Enemies_AreReadApart_AndKeptByFromPieces()
        {
            var layout = LevelLayout.Parse("origin 0 0\n$.@\nAAA\nAAA\n");
            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 2) }, layout.EnemyCells());
            CollectionAssert.IsEmpty(layout.Problems(), "ground under the enemy");
            Assert.IsFalse(layout.Pieces().ContainsKey(new Vector2Int(2, 2)), "an enemy is not ground");

            var again = LevelLayout.FromPieces(layout.Pieces(), layout.CoinCells(), layout.EnemyCells());
            StringAssert.Contains("$.@\nAAA\nAAA\n", again.ToText());
        }

        [Test]
        public void Problems_NameAnEnemyInTheAir()
        {
            var problems = LevelLayout.Parse("origin 0 0\n..@\n...\nAAA\nAAA\n").Problems();
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("no ground under it", problems[0]);
        }

        [Test]
        public void MainLayout_HasNoProblems_AndSavesBackTheSame()
        {
            var path = LevelLayoutMenu.LayoutPath("Main");
            Assert.IsTrue(File.Exists(path), $"no layout file at {path}");
            var layout = LevelLayout.Parse(File.ReadAllText(path));
            CollectionAssert.IsEmpty(layout.Problems(), path);
            var pieces = layout.Pieces();
            CollectionAssert.AreEquivalent(pieces, LevelLayout.FromPieces(pieces).Pieces(), "Save after Build gives the same layout");
            Assert.That(layout.CoinCells(), Is.Not.Empty, "the level should have coins");
            Assert.That(layout.EnemyCells(), Is.Not.Empty, "the level should have an enemy");
        }

        [Test]
        public void GroundTiles_AreAllThere_OnePerPiece()
        {
            var paths = LevelLayoutMenu.TilePaths;
            Assert.AreEqual(9, paths.Count, "3 rows x 3 columns");
            Assert.AreEqual(9, paths.Values.Distinct().Count(), "a different tile for each piece");
            foreach (var path in paths.Values)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TileBase>(path), $"no tile at {path}");
            }
        }
    }
}
