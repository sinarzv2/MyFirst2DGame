using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Editor-only (this folder has its own assembly, MyFirst2DGame.Editor, built for the Editor only:
// a game build has no UnityEditor). Adds Tools > Level to Unity's menu bar, with two ways between
// the level's text map (Assets/Levels/<scene>.txt, see LevelLayout) and the Tilemap of the open scene.
namespace LevelTools
{
    public static class LevelLayoutMenu
    {
        // The ground tile for each piece. Ground.png and GroundFill.png were sliced left to right, top
        // row first: Ground_0..2 = grass left, middle, right; Ground_3..5 = outlined bottom row.
        public static readonly IReadOnlyDictionary<GroundPiece, string> TilePaths = new Dictionary<GroundPiece, string>
        {
            [new GroundPiece(GroundRow.Top, GroundColumn.Left)] = "Assets/Tiles/Ground_0.asset",
            [new GroundPiece(GroundRow.Top, GroundColumn.Middle)] = "Assets/Tiles/Ground_1.asset",
            [new GroundPiece(GroundRow.Top, GroundColumn.Right)] = "Assets/Tiles/Ground_2.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Left)] = "Assets/Tiles/GroundFill_0.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Middle)] = "Assets/Tiles/GroundFill_1.asset",
            [new GroundPiece(GroundRow.Fill, GroundColumn.Right)] = "Assets/Tiles/GroundFill_2.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Left)] = "Assets/Tiles/Ground_3.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Middle)] = "Assets/Tiles/Ground_4.asset",
            [new GroundPiece(GroundRow.Bottom, GroundColumn.Right)] = "Assets/Tiles/Ground_5.asset",
        };

        public static string LayoutPath(string sceneName) => $"Assets/Levels/{sceneName}.txt";

        // Put wherever the layout has a '$', under a GameObject of this name.
        public const string CoinPrefabPath = "Assets/Prefabs/Coin.prefab";
        private const string CoinsParentName = "Coins";

        private const string Title = "Level Layout";

        // Text -> tiles. Replaces every tile of the Tilemap, then fits LevelBounds; one Undo step.
        [MenuItem("Tools/Level/Build From Layout")]
        private static void Build()
        {
            if (!TryFindTilemap(out var tilemap) || !TryLoadTiles(out var tiles))
            {
                return;
            }

            var path = LayoutPath(tilemap.gameObject.scene.name);
            if (!File.Exists(path))
            {
                Fail($"There is no layout file at {path}.\n\nUse Tools > Level > Save Layout From Level to write it from the tiles.");
                return;
            }

            Dictionary<Vector2Int, GroundPiece> wanted;
            List<Vector2Int> wantedCoins;
            try
            {
                var layout = LevelLayout.Parse(File.ReadAllText(path));
                var problems = layout.Problems();
                if (problems.Count > 0)
                {
                    Fail($"{path} has {problems.Count} problem(s):\n" + string.Join("\n", problems.Take(8)));
                    return;
                }

                wanted = layout.Pieces();
                wantedCoins = layout.CoinCells();
            }
            catch (FormatException e)
            {
                Fail($"{path}: {e.Message}");
                return;
            }

            var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
            if (wantedCoins.Count > 0 && coinPrefab == null)
            {
                Fail($"{path} has coins, but there is no coin prefab at {CoinPrefabPath} to place.");
                return;
            }

            // Painting where there is nothing loses nothing; changing or removing a tile or a coin
            // does. Ask first, in case those were placed by hand and never saved to the text.
            ReadPieces(tilemap, tiles, out var current, out var unknown);
            var lost = current.Where(pair => !wanted.TryGetValue(pair.Key, out var piece) || !piece.Equals(pair.Value))
                .Select(pair => pair.Key).Concat(unknown)
                .Concat(CoinCellsInScene(tilemap).Where(cell => !wantedCoins.Contains(cell)))
                .ToList();
            if (lost.Count > 0 && !EditorUtility.DisplayDialog(Title,
                    $"{lost.Count} tile(s) or coin(s) now in the scene are not in {path} and will be changed or removed, " +
                    $"for example at {Cells(lost)}.\n\nIf you placed them by hand, Cancel, then use Save Layout From Level first. " +
                    "(Edit > Undo can bring them back either way.)",
                    "Build anyway", "Cancel"))
            {
                return;
            }

            var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(tilemap, "Build Level From Layout");
            tilemap.ClearAllTiles();
            tilemap.SetTiles(
                wanted.Keys.Select(cell => new Vector3Int(cell.x, cell.y, 0)).ToArray(),
                wanted.Values.Select(piece => tiles[piece]).ToArray());   // a Dictionary lists Keys and Values in the same order
            tilemap.CompressBounds();
            PlaceCoins(tilemap, coinPrefab, wantedCoins);

            var bounds = Object.FindObjectOfType<LevelBounds>();
            if (bounds != null)
            {
                bounds.FitInEditor();
            }

            Undo.CollapseUndoOperations(group);   // one Ctrl+Z undoes the tiles and the bounds together
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
            Debug.Log($"Level: built {wanted.Count} tiles and {wantedCoins.Count} coins from {path}" +
                      (bounds != null ? " and fitted LevelBounds. Save the scene to keep them." : ". No LevelBounds in the scene to fit."));
        }

        // Tiles -> text. Writes the layout of the tiles now in the Tilemap.
        [MenuItem("Tools/Level/Save Layout From Level")]
        private static void Save()
        {
            if (!TryFindTilemap(out var tilemap) || !TryLoadTiles(out var tiles))
            {
                return;
            }

            ReadPieces(tilemap, tiles, out var pieces, out var unknown);
            if (unknown.Count > 0)
            {
                Fail($"{unknown.Count} tile(s) are not one of the nine ground tiles, for example at {Cells(unknown)}: the layout can't hold them.");
                return;
            }

            if (pieces.Count == 0)
            {
                Fail("The Tilemap is empty: there is nothing to save.");
                return;
            }

            // A wrong tile inside a block (say an outlined bottom tile mid-column) is saved as the tile
            // the block needs there, so the next build will replace it. Say so before saving.
            LevelLayout layout;
            try
            {
                layout = LevelLayout.FromPieces(pieces, CoinCellsInScene(tilemap));
            }
            catch (ArgumentException e)
            {
                Fail(e.Message);
                return;
            }

            var problems = layout.Problems();
            var misfits = problems.Count > 0
                ? new List<Vector2Int>()
                : layout.Pieces().Where(pair => !pieces[pair.Key].Equals(pair.Value)).Select(pair => pair.Key).ToList();
            if (problems.Count > 0)
            {
                Fail("The tiles make blocks no layout can hold:\n" + string.Join("\n", problems.Take(8)));
                return;
            }

            if (misfits.Count > 0 && !EditorUtility.DisplayDialog(Title,
                    $"{misfits.Count} tile(s) don't match their block (an edge tile in its middle?), for example at {Cells(misfits)}. " +
                    "The layout will have the tile the block needs there, and the next build will paint it.",
                    "Save anyway", "Cancel"))
            {
                return;
            }

            var path = LayoutPath(tilemap.gameObject.scene.name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, layout.ToText());
            AssetDatabase.ImportAsset(path);
            Debug.Log($"Level: saved {pieces.Count} tiles and {layout.CoinCells().Count} coins to {path}");
        }

        // The cell of every coin in the open scene, hidden ones too (a coin taken in Play Mode is
        // hidden, but Play Mode changes are undone when it ends).
        private static List<Vector2Int> CoinCellsInScene(Tilemap tilemap) =>
            Object.FindObjectsOfType<Coin>(true)
                .Select(coin => (Vector2Int)tilemap.WorldToCell(coin.transform.position))
                .ToList();

        // Replaces every coin in the scene with one prefab instance per cell, under "Coins".
        private static void PlaceCoins(Tilemap tilemap, GameObject prefab, List<Vector2Int> cells)
        {
            foreach (var coin in Object.FindObjectsOfType<Coin>(true))
            {
                Undo.DestroyObjectImmediate(coin.gameObject);
            }

            if (cells.Count == 0)
            {
                return;
            }

            var parent = GameObject.Find(CoinsParentName);
            if (parent == null)
            {
                parent = new GameObject(CoinsParentName);
                Undo.RegisterCreatedObjectUndo(parent, "Build Level From Layout");
            }

            foreach (var cell in cells)
            {
                // InstantiatePrefab, not Instantiate: the copy stays linked to the prefab, so a change
                // to the prefab (its sprite, say) reaches every coin.
                var coin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                coin.transform.position = tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                coin.name = $"Coin ({cell.x}, {cell.y})";
                Undo.RegisterCreatedObjectUndo(coin, "Build Level From Layout");
            }
        }

        private static bool TryFindTilemap(out Tilemap tilemap)
        {
            var tilemaps = Object.FindObjectsOfType<Tilemap>();
            tilemap = tilemaps.Length == 1 ? tilemaps[0] : null;
            if (tilemap == null)
            {
                Fail($"The open scene should have exactly one Tilemap; it has {tilemaps.Length}.");
            }

            return tilemap != null;
        }

        private static bool TryLoadTiles(out Dictionary<GroundPiece, TileBase> tiles)
        {
            // A local first: a lambda may not use an out parameter (error CS1628).
            var loaded = TilePaths.ToDictionary(pair => pair.Key, pair => AssetDatabase.LoadAssetAtPath<TileBase>(pair.Value));
            tiles = loaded;
            var missing = TilePaths.Where(pair => loaded[pair.Key] == null).Select(pair => pair.Value).ToList();
            if (missing.Count > 0)
            {
                Fail("These ground tiles are missing:\n" + string.Join("\n", missing));
            }

            return missing.Count == 0;
        }

        // The piece of every painted cell, and the cells whose tile isn't a ground tile.
        private static void ReadPieces(Tilemap tilemap, Dictionary<GroundPiece, TileBase> tiles,
            out Dictionary<Vector2Int, GroundPiece> pieces, out List<Vector2Int> unknown)
        {
            var pieceOf = tiles.ToDictionary(pair => pair.Value, pair => pair.Key);
            pieces = new Dictionary<Vector2Int, GroundPiece>();
            unknown = new List<Vector2Int>();
            tilemap.CompressBounds();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(cell);
                if (tile == null)
                {
                    continue;
                }

                if (pieceOf.TryGetValue(tile, out var piece))
                {
                    pieces[(Vector2Int)cell] = piece;
                }
                else
                {
                    unknown.Add((Vector2Int)cell);
                }
            }
        }

        private static string Cells(IEnumerable<Vector2Int> cells) =>
            string.Join(", ", cells.Take(5).Select(c => $"({c.x}, {c.y})"));

        private static void Fail(string message)
        {
            Debug.LogError("Level: " + message);
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
