using UnityEngine;

/// <summary>
/// 一个纯净的、可被安全序列化的二维向量。
/// 我只拥有 x 和 y，我发誓我身上再也没有任何会让 Json.Net 发疯的便利属性了！
/// </summary>
[System.Serializable]
public struct SerializableVector2
{
    public float x;
    public float y;

    public SerializableVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// 零向量 (0, 0)
    /// </summary>
    public static SerializableVector2 zero => new SerializableVector2(0f, 0f);

    /// <summary>
    /// 一向量 (1, 1)
    /// </summary>
    public static SerializableVector2 one => new SerializableVector2(1f, 1f);

    /// <summary>
    /// 向量长度的平方 (只读计算属性，不会被序列化)
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public float sqrMagnitude => x * x + y * y;

    /// <summary>
    /// 向量长度 (只读计算属性，不会被序列化)
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public float magnitude => Mathf.Sqrt(sqrMagnitude);

    /// <summary>
    /// 归一化向量 (只读计算属性，不会被序列化)
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public Vector2 normalized
    {
        get
        {
            float mag = magnitude;
            if (mag > 0f)
                return new Vector2(x / mag, y / mag);
            return Vector2.zero;
        }
    }

    // 运算符重载 - 加减乘
    public static SerializableVector2 operator +(SerializableVector2 a, SerializableVector2 b)
    {
        return new SerializableVector2(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2 operator -(SerializableVector2 a, SerializableVector2 b)
    {
        return new SerializableVector2(a.x - b.x, a.y - b.y);
    }

    public static SerializableVector2 operator *(SerializableVector2 a, float b)
    {
        return new SerializableVector2(a.x * b, a.y * b);
    }

    public static SerializableVector2 operator *(float a, SerializableVector2 b)
    {
        return new SerializableVector2(b.x * a, b.y * a);
    }

    // 相等性比较 - 同类型
    public static bool operator ==(SerializableVector2 a, SerializableVector2 b)
    {
        return a.x == b.x && a.y == b.y;
    }

    public static bool operator !=(SerializableVector2 a, SerializableVector2 b)
    {
        return a.x != b.x || a.y != b.y;
    }

    // 运算符重载 - 混合类型 (加减法)
    public static SerializableVector2 operator +(SerializableVector2 a, Vector2 b)
    {
        return new SerializableVector2(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2 operator +(Vector2 a, SerializableVector2 b)
    {
        return new SerializableVector2(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2 operator -(SerializableVector2 a, Vector2 b)
    {
        return new SerializableVector2(a.x - b.x, a.y - b.y);
    }

    public static SerializableVector2 operator -(Vector2 a, SerializableVector2 b)
    {
        return new SerializableVector2(a.x - b.x, a.y - b.y);
    }

    public override bool Equals(object obj)
    {
        if (obj is SerializableVector2)
        {
            var other = (SerializableVector2)obj;
            return this.x == other.x && this.y == other.y;
        }
        if (obj is Vector2)
        {
            var other = (Vector2)obj;
            return this.x == other.x && this.y == other.y;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ (y.GetHashCode() << 2);
    }

    // 隐式转换魔法，让我们的代码依然优雅
    // 注意：== 和 != 比较时请显式转换，如 (Vector2)serializableVec == unityVec
    public static implicit operator SerializableVector2(Vector2 original)
    {
        return new SerializableVector2(original.x, original.y);
    }

    public static implicit operator Vector2(SerializableVector2 serialized)
    {
        return new Vector2(serialized.x, serialized.y);
    }

    public static implicit operator Vector3(SerializableVector2 serialized)
    {
        return new Vector3(serialized.x, serialized.y, 0f);
    }
}

[System.Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    // 隐式转换魔法，让我们的代码依然优雅
    public static implicit operator SerializableVector3(Vector3 original)
    {
        return new SerializableVector3(original.x, original.y, original.z);
    }

    public static implicit operator Vector3(SerializableVector3 serialized)
    {
        return new Vector3(serialized.x, serialized.y, serialized.z);
    }
}

[System.Serializable]
public struct SerializableVector2Int
{
    public int x;
    public int y;

    public SerializableVector2Int(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// 零向量 (0, 0)
    /// </summary>
    public static SerializableVector2Int zero => new SerializableVector2Int(0, 0);

    /// <summary>
    /// 一向量 (1, 1)
    /// </summary>
    public static SerializableVector2Int one => new SerializableVector2Int(1, 1);

    // 运算符重载 - 加减法
    public static SerializableVector2Int operator +(SerializableVector2Int a, SerializableVector2Int b)
    {
        return new SerializableVector2Int(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2Int operator -(SerializableVector2Int a, SerializableVector2Int b)
    {
        return new SerializableVector2Int(a.x - b.x, a.y - b.y);
    }

    // 相等性比较 - 同类型
    public static bool operator ==(SerializableVector2Int a, SerializableVector2Int b)
    {
        return a.x == b.x && a.y == b.y;
    }

    public static bool operator !=(SerializableVector2Int a, SerializableVector2Int b)
    {
        return a.x != b.x || a.y != b.y;
    }

    // 运算符重载 - 混合类型 (加减法)
    public static SerializableVector2Int operator +(SerializableVector2Int a, Vector2Int b)
    {
        return new SerializableVector2Int(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2Int operator +(Vector2Int a, SerializableVector2Int b)
    {
        return new SerializableVector2Int(a.x + b.x, a.y + b.y);
    }

    public static SerializableVector2Int operator -(SerializableVector2Int a, Vector2Int b)
    {
        return new SerializableVector2Int(a.x - b.x, a.y - b.y);
    }

    public static SerializableVector2Int operator -(Vector2Int a, SerializableVector2Int b)
    {
        return new SerializableVector2Int(a.x - b.x, a.y - b.y);
    }

    public override bool Equals(object obj)
    {
        if (obj is SerializableVector2Int)
        {
            var other = (SerializableVector2Int)obj;
            return this.x == other.x && this.y == other.y;
        }
        if (obj is Vector2Int)
        {
            var other = (Vector2Int)obj;
            return this.x == other.x && this.y == other.y;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return x ^ (y << 2);
    }

    // 隐式转换魔法，让我们的代码依然优雅
    // 注意：== 和 != 比较时请显式转换，如 (Vector2Int)serializableVec == unityVec
    public static implicit operator SerializableVector2Int(Vector2Int original)
    {
        return new SerializableVector2Int(original.x, original.y);
    }

    public static implicit operator Vector2Int(SerializableVector2Int serialized)
    {
        return new Vector2Int(serialized.x, serialized.y);
    }
}

[System.Serializable]
public struct SerializableVector3Int
{
    public int x;
    public int y;
    public int z;

    public SerializableVector3Int(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    // 隐式转换魔法，让我们的代码依然优雅
    public static implicit operator SerializableVector3Int(Vector3Int original)
    {
        return new SerializableVector3Int(original.x, original.y, original.z);
    }

    public static implicit operator Vector3Int(SerializableVector3Int serialized)
    {
        return new Vector3Int(serialized.x, serialized.y, serialized.z);
    }
}

[System.Serializable]
public struct SerializableQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public SerializableQuaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    // 隐式转换魔法，让我们的代码依然优雅
    public static implicit operator SerializableQuaternion(Quaternion original)
    {
        return new SerializableQuaternion(original.x, original.y, original.z, original.w);
    }

    public static implicit operator Quaternion(SerializableQuaternion serialized)
    {
        return new Quaternion(serialized.x, serialized.y, serialized.z, serialized.w);
    }
}
