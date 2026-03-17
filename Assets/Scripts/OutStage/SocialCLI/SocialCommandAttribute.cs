using System;

/// <summary>
/// 社交命令标记特性
/// <para>纯标记特性，用于标识哪些命令可以在社交终端执行喵~</para>
/// <para>被标记的命令会自动加入 SocialCLI 的白名单中</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SocialCommandAttribute : Attribute
{
    // 纯标记，无字段
}
