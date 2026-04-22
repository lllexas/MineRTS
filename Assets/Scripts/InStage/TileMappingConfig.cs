using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "TileMappingConfig", menuName = "MineRTS/Tile Mapping Config")]
public class TileMappingConfig : ScriptableObject
{
    [System.Serializable]
    public struct TileMappingEntry
    {
        public TileBase tileAsset;
        public int tileID;
    }

    [SerializeField] private List<TileMappingEntry> _entries = new List<TileMappingEntry>();

    public IReadOnlyList<TileMappingEntry> Entries => _entries;

    public void SetEntries(List<TileMappingEntry> entries)
    {
        _entries = entries ?? new List<TileMappingEntry>();
    }

    public int GetTileID(TileBase tile)
    {
        if (tile == null)
        {
            return 0;
        }

        foreach (var entry in _entries)
        {
            if (entry.tileAsset == tile)
            {
                return entry.tileID;
            }
        }

        return 0;
    }

    public TileBase GetTileAsset(int tileId)
    {
        if (tileId == 0)
        {
            return null;
        }

        foreach (var entry in _entries)
        {
            if (entry.tileID == tileId)
            {
                return entry.tileAsset;
            }
        }

        return null;
    }

    public void PopulateDictionaries(Dictionary<TileBase, int> assetToId, Dictionary<int, TileBase> idToAsset)
    {
        assetToId.Clear();
        idToAsset.Clear();

        foreach (var entry in _entries)
        {
            if (entry.tileAsset == null)
            {
                continue;
            }

            assetToId[entry.tileAsset] = entry.tileID;
            idToAsset[entry.tileID] = entry.tileAsset;
        }
    }
}
