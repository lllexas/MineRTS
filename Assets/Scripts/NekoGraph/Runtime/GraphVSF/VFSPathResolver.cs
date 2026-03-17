using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSPathResolver - VFS 路径解析器（静态工具类）喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 设计哲学：
/// - 类似 Linux 的路径处理
/// - 支持绝对路径和相对路径
/// - 支持 ..（上级目录）和 .（当前目录）
///
/// 示例：
/// - Normalize("/social/friends") → "/social/friends/"
/// - Combine("/social/", "friends/") → "/social/friends/"
/// - Resolve("/social/", "..") → "/"
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class VFSPathResolver
{
    /// <summary>
    /// 规范化路径
    ///
    /// 规则：
    /// - 确保以 / 开头和结尾
    /// - 替换多个连续的 / 为单个 /
    /// - 移除首尾空格
    ///
    /// 示例：
    /// - "/social/friends" → "/social/friends/"
    /// - "social//friends" → "/social/friends/"
    /// - "  /social/  " → "/social/"
    /// </summary>
    /// <param name="path">输入路径</param>
    /// <returns>规范化后的路径</returns>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";

        path = path.Trim().Replace("\\", "/");

        // 确保以/开头
        if (!path.StartsWith("/"))
            path = "/" + path;

        // 替换多个/为单个
        while (path.Contains("//"))
            path = path.Replace("//", "/");

        // 只有根目录 "/" 强制保留结尾斜杠
        // 其他路径的结尾斜杠由业务逻辑（是否是目录）决定，这里不强制添加喵~
        if (path.Length > 1 && path.EndsWith("/"))
        {
            // 如果路径不是 "/"，我们暂时保留它，不做删除，也不做强加喵~
        }

        return path;
    }

    /// <summary>
    /// 合并路径（类似 Path.Combine）
    ///
    /// 示例：
    /// - Combine("/social/", "friends/") → "/social/friends/"
    /// - Combine("/social/", "../") → "/"
    /// - Combine("/social/", "/config/") → "/config/"（绝对路径）
    /// </summary>
    /// <param name="basePath">基础路径</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>合并后的路径</returns>
    public static string Combine(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return Normalize(basePath);
        if (string.IsNullOrEmpty(basePath)) return Normalize(relativePath);

        // 绝对路径直接返回
        if (relativePath.StartsWith("/"))
            return Normalize(relativePath);

        // 处理特殊路径
        if (relativePath == ".") return Normalize(basePath);
        if (relativePath == "..") return GetParentPath(basePath);

        // 移除开头的 ./
        if (relativePath.StartsWith("./"))
            relativePath = relativePath.Substring(2);

        // 合并
        string result = basePath;
        if (!result.EndsWith("/"))
            result += "/";
        result += relativePath;

        return Normalize(result);
    }

    /// <summary>
    /// 获取父路径
    ///
    /// 示例：
    /// - "/social/friends/" → "/social/"
    /// - "/social/" → "/"
    /// - "/" → "/"
    /// </summary>
    /// <param name="path">输入路径</param>
    /// <returns>父路径</returns>
    public static string GetParentPath(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";

        path = path.TrimEnd('/');
        int lastSlash = path.LastIndexOf('/');

        if (lastSlash <= 0) return "/";
        return path.Substring(0, lastSlash + 1);
    }

    /// <summary>
    /// 获取路径最后一段（文件名/目录名）
    ///
    /// 示例：
    /// - "/social/friends/" → "friends"
    /// - "/social/" → "social"
    /// - "/" → ""
    /// </summary>
    /// <param name="path">输入路径</param>
    /// <returns>路径最后一段</returns>
    public static string GetFileName(string path)
    {
        path = Normalize(path);
        if (path == "/") return "";

        path = path.TrimEnd('/');
        int lastSlash = path.LastIndexOf('/');

        if (lastSlash < 0 || lastSlash >= path.Length - 1)
            return path;

        return path.Substring(lastSlash + 1);
    }

    /// <summary>
    /// 分割路径段
    ///
    /// 示例：
    /// - "/social/friends/" → ["social", "friends"]
    /// - "/" → []
    /// </summary>
    /// <param name="path">输入路径</param>
    /// <returns>路径段数组</returns>
    public static string[] SplitToSegments(string path)
    {
        path = Normalize(path);
        if (path == "/") return new string[0];

        path = path.Trim('/');
        if (string.IsNullOrEmpty(path)) return new string[0];

        return path.Split('/');
    }

    /// <summary>
    /// 从路径段构建路径
    ///
    /// 示例：
    /// - FromSegments(["social", "friends"]) → "/social/friends/"
    /// - FromSegments([]) → "/"
    /// </summary>
    /// <param name="segments">路径段集合</param>
    /// <returns>构建的路径</returns>
    public static string FromSegments(IEnumerable<string> segments)
    {
        if (segments == null || !segments.Any()) return "/";
        return "/" + string.Join("/", segments.Where(s => !string.IsNullOrEmpty(s))) + "/";
    }

    /// <summary>
    /// 解析路径（支持相对路径）
    ///
    /// 示例：
    /// - Resolve("/social/", "friends") → "/social/friends/"
    /// - Resolve("/social/friends/", "..") → "/social/"
    /// - Resolve("/social/", "/config/") → "/config/"
    /// </summary>
    /// <param name="currentPath">当前路径</param>
    /// <param name="inputPath">输入路径（可以是相对或绝对）</param>
    /// <returns>解析后的路径</returns>
    public static string Resolve(string currentPath, string inputPath)
    {
        if (string.IsNullOrEmpty(inputPath)) return Normalize(currentPath);
        if (inputPath.StartsWith("/")) return Normalize(inputPath);
        return Combine(currentPath, inputPath);
    }

    /// <summary>
    /// 解析路径（支持 .. 和 . 特殊路径）
    ///
    /// 示例：
    /// - ResolveWithSpecial("/social/friends/", "../requests/") → "/social/requests/"
    /// - ResolveWithSpecial("/social/", "./friends/") → "/social/friends/"
    /// </summary>
    /// <param name="currentPath">当前路径</param>
    /// <param name="inputPath">输入路径（可包含 .. 和 .）</param>
    /// <returns>解析后的路径</returns>
    public static string ResolveWithSpecial(string currentPath, string inputPath)
    {
        if (string.IsNullOrEmpty(inputPath)) return Normalize(currentPath);
        
        // 绝对路径
        if (inputPath.StartsWith("/"))
            return Normalize(inputPath);

        // 分割路径段
        var currentSegments = SplitToSegments(currentPath).ToList();
        var inputSegments = SplitToSegments(inputPath).ToList();

        foreach (var segment in inputSegments)
        {
            if (segment == ".")
            {
                // 当前目录，跳过
                continue;
            }
            else if (segment == "..")
            {
                // 上级目录
                if (currentSegments.Count > 0)
                    currentSegments.RemoveAt(currentSegments.Count - 1);
            }
            else
            {
                // 普通路径段
                currentSegments.Add(segment);
            }
        }

        return FromSegments(currentSegments);
    }
}
