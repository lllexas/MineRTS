[System.Serializable]
public class LevelMapData
{
    public string levelId;
    public int width;
    public int height;

    // 🔥【新增】记录地图左下角的坐标偏移
    public int originX;
    public int originY;

    public int[] groundMap;
    public int[] gridMap;
    public int[] effectMap;
}