#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
VacantRoomWeb 项目代码打包脚本
用于生成适合上传到Claude的房间管理系统代码文档
✨ 包含敏感信息保护功能
"""

import os
import glob
import json
import re
from pathlib import Path
from datetime import datetime

# ✨增强的掩码函数
def mask_value(val: str, min_show: int = None) -> str:
    """
    根据字符串长度智能掩码
    - 短值（≤8字符）：显示前4位 + ****
    - 中等值（9-16字符）：显示前8位 + ****
    - 长值（>16字符）：显示前16位 + ****
    """
    if not val:
        return val
    
    if min_show is not None:
        show_chars = min_show
    elif len(val) <= 8:
        show_chars = min(4, len(val))
    elif len(val) <= 16:
        show_chars = 8
    else:
        show_chars = 16
    
    if len(val) <= show_chars:
        return val
    return val[:show_chars] + '****'

# ✨敏感信息关键词列表
SENSITIVE_KEYWORDS = [
    'password', 'pwd', 'passwd', 'secret', 'key', 'token', 'apikey', 'api_key',
    'connectionstring', 'connstr', 'connection_string', 'hash', 'salt', 
    'signature', 'private', 'credential', 'auth', 'jwt', 'bearer',
    'database', 'server', 'userid', 'user_id', 'username', 'admin'
]

def is_sensitive_key(key: str) -> bool:
    """检查键名是否包含敏感关键词"""
    key_lower = key.lower()
    return any(keyword in key_lower for keyword in SENSITIVE_KEYWORDS)

def mask_connection_string(conn_str: str) -> str:
    """智能处理连接字符串，只掩码敏感部分"""
    if not conn_str:
        return conn_str
    
    # 处理各种连接字符串格式
    patterns = [
        (r'(password|pwd)\s*=\s*([^;]+)', r'\1=****'),
        (r'(user\s*id|uid|username)\s*=\s*([^;]+)', lambda m: f'{m.group(1)}={mask_value(m.group(2), 4)}'),
        (r'(server|data\s*source)\s*=\s*([^;]+)', lambda m: f'{m.group(1)}={mask_value(m.group(2), 8)}'),
    ]
    
    result = conn_str
    for pattern, replacement in patterns:
        if callable(replacement):
            result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
        else:
            result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)
    
    return result

def process_json_content(content: str) -> str:
    """处理JSON文件中的敏感信息"""
    try:
        data = json.loads(content)
        processed_data = mask_json_recursive(data)
        return json.dumps(processed_data, indent=2, ensure_ascii=False)
    except json.JSONDecodeError:
        return content

def mask_json_recursive(obj):
    """递归处理JSON对象中的敏感信息"""
    if isinstance(obj, dict):
        result = {}
        for key, value in obj.items():
            if isinstance(value, str) and is_sensitive_key(key):
                # 特殊处理连接字符串
                if 'connection' in key.lower():
                    result[key] = mask_connection_string(value)
                else:
                    result[key] = mask_value(value)
            elif isinstance(value, (dict, list)):
                result[key] = mask_json_recursive(value)
            else:
                result[key] = value
        return result
    elif isinstance(obj, list):
        return [mask_json_recursive(item) for item in obj]
    else:
        return obj

def process_csharp_content(content: str) -> str:
    """处理C#代码中的敏感信息"""
    lines = content.split('\n')
    processed_lines = []
    
    for line in lines:
        # 处理字符串字面量赋值
        # 如: string password = "secret123";
        string_assignment_pattern = r'(\w*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')\w*)\s*=\s*["\']([^"\']+)["\']'
        
        def replace_assignment(match):
            var_name, value = match.groups()
            if is_sensitive_key(var_name):
                return f'{var_name} = "{mask_value(value)}"'
            return match.group(0)
        
        processed_line = re.sub(string_assignment_pattern, replace_assignment, line, flags=re.IGNORECASE)
        
        # 处理常量定义
        # 如: const string API_KEY = "abc123";
        const_pattern = r'(const\s+string\s+\w*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')\w*\s*=\s*["\'])([^"\']+)(["\'])'
        
        def replace_const(match):
            prefix, value, suffix = match.groups()
            return f'{prefix}{mask_value(value)}{suffix}'
        
        processed_line = re.sub(const_pattern, replace_const, processed_line, flags=re.IGNORECASE)
        
        # 处理配置访问
        # 如: Configuration["ConnectionStrings:Default"]
        config_pattern = r'(Configuration\[["\'][^"\']*(?:' + '|'.join(SENSITIVE_KEYWORDS) + r')[^"\']*["\']]\s*=\s*["\'])([^"\']+)(["\'])'
        
        def replace_config(match):
            prefix, value, suffix = match.groups()
            return f'{prefix}{mask_value(value)}{suffix}'
        
        processed_line = re.sub(config_pattern, replace_config, processed_line, flags=re.IGNORECASE)
        
        processed_lines.append(processed_line)
    
    return '\n'.join(processed_lines)

def process_config_content(content: str) -> str:
    """处理其他配置文件中的敏感信息"""
    # 处理 key=value 格式
    lines = content.split('\n')
    processed_lines = []
    
    for line in lines:
        # 匹配 key=value 或 key:value 格式
        kv_pattern = r'^(\s*)([^=:]+)[=:](.+)$'
        match = re.match(kv_pattern, line.strip())
        
        if match:
            indent, key, value = match.groups()
            if is_sensitive_key(key.strip()):
                processed_line = f'{indent}{key.strip()}={mask_value(value.strip())}'
            else:
                processed_line = line
        else:
            processed_line = line
        
        processed_lines.append(processed_line)
    
    return '\n'.join(processed_lines)

def process_file_content(file_path: str, content: str) -> str:
    """根据文件类型处理敏感信息"""
    file_ext = os.path.splitext(file_path)[1].lower()
    file_name = os.path.basename(file_path).lower()
    
    if file_ext == '.json':
        # 处理 JSON 配置文件
        return process_json_content(content)
    elif file_ext == '.cs':
        # 处理 C# 代码文件
        return process_csharp_content(content)
    elif file_ext in ['.config', '.xml'] and ('web.config' in file_name or 'app.config' in file_name):
        # 处理 XML 配置文件中的环境变量
        def replacer(m):
            name, val = m.group(1), m.group(2)
            return f'<environmentVariable name="{name}" value="{mask_value(val)}" />'
        
        # 处理环境变量
        pattern = re.compile(r'<environmentVariable\s+name="([^"]+)"\s+value="([^"]+)"\s*/>')
        content = pattern.sub(replacer, content)
        
        # 处理其他配置项
        content = process_config_content(content)
        
        return content
    elif file_ext in ['.properties', '.ini', '.env']:
        # 处理其他配置文件格式
        return process_config_content(content)
    else:
        return content

def get_file_size_from_bytes(size_bytes):
    """将字节数转换为人类可读格式"""
    if size_bytes < 1024:
        return f"{size_bytes} B"
    elif size_bytes < 1024 * 1024:
        return f"{size_bytes/1024:.1f} KB"
    else:
        return f"{size_bytes/(1024*1024):.1f} MB"

def get_file_extension_for_syntax(file_path):
    """根据文件路径返回语法高亮的语言标识"""
    ext = os.path.splitext(file_path)[1].lower()
    
    syntax_map = {
        '.cs': 'csharp',
        '.razor': 'razor',
        '.css': 'css',
        '.js': 'javascript',
        '.json': 'json',
        '.csproj': 'xml',
        '.config': 'xml',
        '.xml': 'xml',
        '.md': 'markdown',
        '.txt': 'text'
    }
    
    return syntax_map.get(ext, 'text')

def should_skip_file(filename):
    """判断是否应该跳过文件"""
    skip_patterns = [
        ".tmp", ".temp", ".bak", ".old", ".user", ".suo", ".cache",
        ".dll", ".exe", ".pdb", ".deps.json"
    ]
    
    skip_files = [
        "bootstrap.min.css",
        "bootstrap.min.css.map"
    ]
    
    # 检查文件扩展名
    for pattern in skip_patterns:
        if filename.lower().endswith(pattern):
            return True
    
    # 检查特定文件名
    if filename in skip_files:
        return True
        
    return False

def get_target_folders():
    """获取需要扫描的目标文件夹"""
    return [
        "Components", 
        "Pages", 
        "Layout",
        "Services",
        "Data",
        "Models",
        "Controllers",
        "Middleware",
        "Extensions",
        "Utils",
        "Helpers",
        "wwwroot",
        "Properties"
    ]

def combine_code_files():
    # Base directory of your project
    base_dir = r"D:\Programing\C#\VacantRoomWeb\VacantRoomWeb"

    # Output text file
    output_file = os.path.join(r"D:\Programing\C#\VacantRoomWeb", "#all_code_files.txt")

    file_patterns = [
        "*.cs", "*.css", "*.razor", "*.csproj", "*.json", "*.config", "*.xml",
        "*.md", "*.txt", "Components/**/*.razor", "Components/**/*.css",
        "Pages/**/*.razor", "Layout/**/*.razor", "Layout/**/*.css",
        "Services/**/*.cs", "wwwroot/**/*.css", "wwwroot/**/*.js",
        "Properties/**/*.json", "Properties/**/*.xml",
    ]

    exclude_patterns = [
        "bin/**","obj/**",".vs/**","*.user","*.cache","*.tmp","*.log",
        "bootstrap.min.css","bootstrap.min.css.map",
        "node_modules/**","packages/**",".git/**","Logs/**","all_code_files.txt"
    ]

    # 检查目录是否存在
    if not os.path.exists(base_dir):
        print(f"错误: 目录不存在: {base_dir}")
        print("请修改脚本中的 base_dir 路径")
        
        # 尝试父目录
        parent_dir = r"D:\Programing\C#\VacantRoomWeb"
        if os.path.exists(parent_dir):
            print(f"发现父目录: {parent_dir}")
            print("父目录内容:")
            for item in os.listdir(parent_dir):
                item_path = os.path.join(parent_dir, item)
                if os.path.isdir(item_path):
                    print(f"  📁 {item}/")
                else:
                    print(f"  📄 {item}")
        return

    print(f"扫描目录: {base_dir}")
    print(f"输出文件: {output_file}")
    
    # 统计变量
    processed_files = 0
    total_size = 0
    protected_files = 0  # ✨统计被保护的文件数
    target_folders = get_target_folders()

    current_time = datetime.now().strftime("%Y年%m月%d日 %H:%M")

    with open(output_file, 'w', encoding='utf-8') as outf:
        # ✨写入项目描述
        outf.write("# VacantRoomWeb - 空房间管理系统\n")
        outf.write("## 项目概述\n")
        outf.write("基于 Blazor Server 的房间管理系统，提供房间状态监控、预订管理等功能\n")
        outf.write("⚠️  敏感信息已自动掩码处理，保护密码、密钥、连接字符串等\n\n")
        
        outf.write("=" * 80 + "\n")
        outf.write(f"打包时间: {current_time}\n")
        outf.write("VACANTROOM WEB PROJECT - ALL CODE FILES\n")
        outf.write("⚠️  敏感信息已自动掩码处理\n")
        outf.write("=" * 80 + "\n\n")

        original_dir = os.getcwd()
        os.chdir(base_dir)

        try:
            # ✨收集所有文件信息
            all_files = []
            folder_stats = {}  # 统计每个文件夹的文件数
            
            print("开始扫描文件...")
            
            for pattern in file_patterns:
                matching_files = glob.glob(pattern, recursive=True)
                for file_path in matching_files:
                    if os.path.isfile(file_path):
                        normalized_path = file_path.replace('\\', '/')
                        
                        # 检查是否需要排除
                        should_exclude = False
                        for exclude_pattern in exclude_patterns:
                            if exclude_pattern.endswith('/**'):
                                if normalized_path.startswith(exclude_pattern[:-3] + '/'):
                                    should_exclude = True
                                    break
                            elif (exclude_pattern in normalized_path or 
                                  os.path.basename(file_path) == exclude_pattern):
                                should_exclude = True
                                break
                        
                        if should_exclude or should_skip_file(os.path.basename(file_path)):
                            continue
                        
                        # 确定文件夹
                        folder_name = "根目录"
                        for folder in target_folders:
                            if normalized_path.startswith(folder + '/'):
                                folder_name = folder
                                break
                        
                        if folder_name == "根目录" and '/' in normalized_path:
                            continue  # 跳过不在目标文件夹的文件
                        
                        file_size = os.path.getsize(file_path)
                        all_files.append({
                            'path': normalized_path,
                            'folder': folder_name,
                            'size': file_size
                        })
                        
                        # 统计文件夹
                        if folder_name not in folder_stats:
                            folder_stats[folder_name] = 0
                        folder_stats[folder_name] += 1

            all_files = sorted(all_files, key=lambda x: (x['folder'], x['path']))
            
            print(f"找到 {len(all_files)} 个文件进行打包")

            # ✨写入文件索引
            outf.write("## 文件索引\n")
            current_folder = ""
            for file_info in all_files:
                folder = file_info['folder']
                if folder != current_folder:
                    current_folder = folder
                    file_count = folder_stats.get(folder, 0)
                    outf.write(f"\n### {folder} ({file_count} 个文件)\n")
                
                size_str = get_file_size_from_bytes(file_info['size'])
                outf.write(f"- {file_info['path']} ({size_str})\n")

            # ✨写入项目统计
            outf.write(f"\n## 项目统计\n")
            outf.write(f"- 总文件数: {len(all_files)}\n")
            outf.write(f"- 总大小: {get_file_size_from_bytes(sum(f['size'] for f in all_files))}\n")
            outf.write(f"- 生成时间: {current_time}\n")
            outf.write(f"- 项目路径: {base_dir}\n")
            
            # ✨写入技术栈信息
            outf.write(f"\n## 技术栈\n")
            outf.write("- ASP.NET Core Blazor Server\n")
            outf.write("- Entity Framework Core\n")
            outf.write("- Bootstrap CSS Framework\n")
            outf.write("- SignalR (实时通信)\n")
            
            # ✨写入文件夹统计
            outf.write(f"\n## 文件夹统计\n")
            for folder, count in sorted(folder_stats.items()):
                outf.write(f"- {folder}: {count} 个文件\n")

            # ✨写入文件内容
            outf.write("\n" + "="*80 + "\n")
            outf.write("## 文件内容\n")
            outf.write("="*80 + "\n")

            current_folder = ""
            for file_info in all_files:
                folder = file_info['folder']
                if folder != current_folder:
                    current_folder = folder
                    outf.write(f"\n\n### {folder} 文件夹\n")
                    outf.write("-" * 50 + "\n")

                try:
                    outf.write(f"\n#### 文件: {file_info['path']}\n")
                    outf.write(f"```{get_file_extension_for_syntax(file_info['path'])}\n")
                    
                    with open(file_info['path'], 'r', encoding='utf-8', errors='ignore') as inf:
                        file_content = inf.read()
                    
                    # ✨处理敏感信息
                    original_content = file_content
                    file_content = process_file_content(file_info['path'], file_content)
                    
                    # 统计是否有敏感信息被保护
                    if file_content != original_content:
                        protected_files += 1
                    
                    outf.write(file_content + "\n\n")
                    outf.write("```\n")
                    
                    # 统计文件大小
                    processed_files += 1
                    total_size += len(file_content.encode('utf-8'))
                    
                except Exception as e:
                    outf.write(f"[ERROR: 无法读取文件 - {str(e)}]\n```\n")
                    processed_files += 1

            # ✨写入保护统计
            outf.write("\n" + "=" * 80 + "\n")
            outf.write("代码文件结束\n")
            outf.write("✅ 敏感信息保护：密码、密钥、连接字符串等已自动掩码\n")
            outf.write(f"📊 统计信息：包含文件总数 {processed_files} 个\n")
            outf.write(f"🔒 敏感信息保护：{protected_files} 个文件\n")
            outf.write("=" * 80 + "\n")

        finally:
            os.chdir(original_dir)
    
    # 计算输出文件大小并显示统计信息
    if os.path.exists(output_file):
        output_size = os.path.getsize(output_file)
        
        print("\n" + "=" * 60)
        print("✅ 所有文件合并成功!")
        print(f"输出文件: {output_file}")
        print(f"文件大小: {get_file_size_from_bytes(output_size)}")
        print(f"包含文件总数: {processed_files}")
        print(f"敏感信息保护: {protected_files} 个文件")
        print("=" * 60)
        print("\n✅ 打包完成！敏感信息已自动保护，可以安全上传到Claude进行代码分析。")

if __name__ == "__main__":
    try:
        combine_code_files()
    except Exception as e:
        print(f"❌ 错误: {e}")
        input()