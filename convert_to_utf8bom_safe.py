#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量将 GB2312/GBK 编码的文件转换为 UTF-8 BOM 编码
安全版本 - 优先检测 UTF-8，避免破坏原有汉字编码喵！(=^･ω･^=)
"""

import os
import sys
import shutil
from pathlib import Path

# 需要转换的文件扩展名
TARGET_EXTENSIONS = {
    '.cs', '.txt', '.md', '.xml', '.json', 
    '.shader', '.compute', '.hlsl', '.glsl',
    '.py', '.js', '.ts', '.yaml', '.yml',
    '.ini', '.cfg', '.config'
}

# 需要排除的目录
EXCLUDE_DIRS = {
    'Library', 'Temp', 'obj', 'bin', '.git', 
    'Packages', 'Logs', 'Build'
}

def is_binary(data):
    """检测是否为二进制文件喵~"""
    return b'\x00' in data

def detect_encoding_safe(data):
    """
    安全检测文件编码喵~
    优先级：UTF-8 BOM > UTF-8 > GBK
    返回：'utf-8-bom', 'utf-8', 'gbk', 或 None
    """
    # 检查 UTF-8 BOM (已有 BOM，不需要转换)
    if data.startswith(b'\xef\xbb\xbf'):
        try:
            data[3:].decode('utf-8')
            return 'utf-8-bom'
        except:
            pass
    
    # 检查 UTF-16 BOM (需要特殊处理)
    if data.startswith(b'\xff\xfe') or data.startswith(b'\xfe\xff'):
        return 'utf-16'
    
    # ★★★ 优先尝试 UTF-8 解码 ★★★
    try:
        data.decode('utf-8')
        return 'utf-8'  # UTF-8 无 BOM，需要添加 BOM
    except:
        pass
    
    # 最后尝试 GBK 解码 (GB2312 是 GBK 的子集)
    try:
        text = data.decode('gbk')
        # 验证是否真的包含中文字符，避免误判
        if any('\u4e00' <= c <= '\u9fff' for c in text):
            return 'gbk'
    except:
        pass
    
    return None

def convert_file(file_path, root_dir=None, backup_dir=None, dry_run=False):
    """
    安全转换单个文件为 UTF-8 BOM 编码喵~
    """
    try:
        with open(file_path, 'rb') as f:
            data = f.read()
        
        # 跳过二进制文件
        if is_binary(data):
            return None, "跳过 (二进制文件)"
        
        encoding = detect_encoding_safe(data)
        
        if encoding == 'utf-8-bom':
            return None, "已是 UTF-8 BOM"
        
        if encoding == 'utf-8':
            # 只需添加 BOM，不重新编码
            utf8_bom = b'\xef\xbb\xbf' + data
            if not dry_run:
                # 先备份
                if backup_dir and root_dir:
                    rel_path = file_path.relative_to(root_dir)
                    backup_path = root_dir / backup_dir / rel_path
                    backup_path.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(file_path, backup_path)
                # 写入带 BOM 的文件
                with open(file_path, 'wb') as f:
                    f.write(utf8_bom)
            return True, f"UTF-8 -> UTF-8 BOM (添加 BOM)"

        if encoding == 'gbk':
            # 需要从 GBK 转换为 UTF-8 BOM
            text = data.decode('gbk')
            utf8_bom = b'\xef\xbb\xbf' + text.encode('utf-8')

            if not dry_run:
                # 先备份
                if backup_dir and root_dir:
                    rel_path = file_path.relative_to(root_dir)
                    backup_path = root_dir / backup_dir / rel_path
                    backup_path.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(file_path, backup_path)
                # 写入转换后的文件
                with open(file_path, 'wb') as f:
                    f.write(utf8_bom)

            return True, f"GBK -> UTF-8 BOM (编码转换)"
        
        return None, f"未知编码或无需处理"
        
    except Exception as e:
        return False, f"错误：{str(e)}"

def main():
    """主函数喵~"""
    root_dir = Path(__file__).parent
    
    print("=" * 60)
    print("GB2312/GBK -> UTF-8 BOM 批量转换工具 (安全版) 喵~ (=^･ω･^=)")
    print("=" * 60)
    
    # 检查参数
    dry_run = '--dry-run' in sys.argv or '-d' in sys.argv
    no_backup = '--no-backup' in sys.argv
    
    # 检查是否传入单个文件路径喵~
    single_file = None
    for arg in sys.argv[1:]:
        if not arg.startswith('--'):
            single_file = Path(arg)
            if not single_file.is_absolute():
                single_file = root_dir / single_file
            break
    
    backup_dir = Path('_encoding_backup')
    
    if dry_run:
        print("\n[预览模式] 不会实际修改文件喵~\n")
    elif not no_backup:
        print(f"\n备份目录：{backup_dir}\n")
    
    if single_file:
        # 单文件模式喵~
        if not single_file.exists():
            print(f"\n错误：文件不存在喵！{single_file}")
            return
        print(f"\n[单文件模式] 处理文件：{single_file}\n")
    
    converted_count = 0
    skipped_count = 0
    error_count = 0
    gbk_count = 0
    
    # 单文件模式喵~
    if single_file:
        result, message = convert_file(single_file, root_dir, backup_dir, dry_run)
        print(f"[{'✓' if result is True else '·' if result is None else '✗'}] {single_file}: {message}")
        if result is True:
            converted_count = 1
            if 'GBK' in message:
                gbk_count = 1
        elif result is False:
            error_count = 1
        else:
            skipped_count = 1
    else:
        # 批量模式喵~
        for root, dirs, files in os.walk(root_dir):
            # 排除不需要的目录
            dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
            
            for file in files:
                file_path = Path(root) / file
                
                # 检查扩展名
                if file_path.suffix.lower() not in TARGET_EXTENSIONS:
                    continue
                
                # 跳过备份目录
                if '_encoding_backup' in str(file_path):
                    continue
                
                # 转换文件
                result, message = convert_file(file_path, root_dir, backup_dir, dry_run)
                
                rel_path = file_path.relative_to(root_dir)
                
                if result is True:
                    print(f"[✓] {rel_path}: {message}")
                    converted_count += 1
                    if 'GBK' in message:
                        gbk_count += 1
                elif result is False:
                    print(f"[✗] {rel_path}: {message}")
                    error_count += 1
                else:
                    print(f"[·] {rel_path}: {message}")
                    skipped_count += 1
    
    print("\n" + "=" * 60)
    print(f"转换完成喵~ (=^･ω･^=)")
    print(f"  已转换：{converted_count} 个文件")
    print(f"    - 其中 GBK 编码转换：{gbk_count} 个文件")
    print(f"  已跳过：{skipped_count} 个文件")
    print(f"  错误：{error_count} 个文件")
    print("=" * 60)
    
    if dry_run:
        print("\n提示：实际运行请去掉 --dry-run 参数喵~")
    elif not no_backup and converted_count > 0:
        print(f"\n备份已保存到：{backup_dir} 喵~")
        print("确认无误后可手动删除备份目录喵！(=^･ω･^=)")

if __name__ == '__main__':
    main()
