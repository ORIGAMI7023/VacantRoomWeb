import os
import glob
from pathlib import Path
from datetime import datetime

def combine_code_files():
    # Base directory of your project
    base_dir = r"D:\Programing\C#\VacantRoomWeb\VacantRoomWeb"
    
    # Output text file
    output_file = os.path.join(base_dir, "all_code_files.txt")
    
    # 扩展的文件模式 - 包含所有重要的项目文件
    file_patterns = [
        "*.cs",                    # 所有 C# 文件
        "*.css",                   # 所有 CSS 文件  
        "*.razor",                 # 所有 Razor 文件
        "*.csproj",                # 项目文件
        "*.json",                  # 配置文件
        "*.config",                # 配置文件
        "*.xml",                   # XML 配置文件
        "*.md",                    # 文档文件
        "*.txt",                   # 文本文件
        "Components/**/*.razor",   # Razor 组件（递归）
        "Components/**/*.css",     # 组件样式文件
        "Pages/**/*.razor",        # Razor 页面（递归）
        "Layout/**/*.razor",       # 布局文件
        "Layout/**/*.css",         # 布局样式
        "Services/**/*.cs",        # 服务层文件（递归）
        "wwwroot/**/*.css",        # 静态资源 CSS
        "wwwroot/**/*.js",         # 静态资源 JS
        "Properties/**/*.json",    # 属性配置文件
        "Properties/**/*.xml",     # 属性配置文件
    ]
    
    # 排除的文件模式 - 更精确的排除规则
    exclude_patterns = [
        "bin/**",
        "obj/**", 
        ".vs/**",
        "*.user",
        "*.cache",
        "*.tmp",
        "*.log",
        "bootstrap.min.css",       # 排除压缩的 bootstrap
        "bootstrap.min.css.map",   # 排除 source map
        "node_modules/**",
        "packages/**",
        ".git/**",
        "Logs/**",                 # 排除日志文件夹
        "all_code_files.txt"       # 排除之前的打包文件
    ]
    
    print(f"扫描目录: {base_dir}")
    print(f"输出文件: {output_file}")
    
    # 使用正确的时间格式
    current_time = datetime.now().strftime("%Y年%m月%d日 %H:%M")
    
    # 使用 UTF-8 编码写入文件
    with open(output_file, 'w', encoding='utf-8') as outf:
        outf.write("=" * 80 + "\n")
        outf.write(f"打包时间: {current_time}\n")
        outf.write("VACANTROOM WEB PROJECT - ALL CODE FILES\n")
        outf.write("=" * 80 + "\n\n")
        
        # 切换到项目目录
        original_dir = os.getcwd()
        os.chdir(base_dir)
        
        try:
            all_files = []

            # 收集所有匹配的文件
            for pattern in file_patterns:
                print(f"搜索模式: {pattern}")
                matching_files = glob.glob(pattern, recursive=True)
                print(f"  找到 {len(matching_files)} 个文件")
                
                for file_path in matching_files:
                    if os.path.isfile(file_path):
                        # 检查文件是否应该被排除
                        should_exclude = False
                        normalized_path = file_path.replace('\\', '/')
                        
                        for exclude_pattern in exclude_patterns:
                            # 处理通配符模式
                            if exclude_pattern.endswith('/**'):
                                exclude_dir = exclude_pattern[:-3]
                                if normalized_path.startswith(exclude_dir + '/'):
                                    should_exclude = True
                                    print(f"  排除 {file_path} (目录规则: {exclude_pattern})")
                                    break
                            elif '**' in exclude_pattern:
                                # 处理包含 ** 的模式
                                exclude_parts = exclude_pattern.split('**')
                                if len(exclude_parts) == 2:
                                    start_part = exclude_parts[0]
                                    end_part = exclude_parts[1]
                                    if (normalized_path.startswith(start_part) and 
                                        (not end_part or normalized_path.endswith(end_part))):
                                        should_exclude = True
                                        print(f"  排除 {file_path} (通配符规则: {exclude_pattern})")
                                        break
                            else:
                                # 精确匹配文件名或路径包含
                                if (os.path.basename(file_path) == exclude_pattern or 
                                    exclude_pattern in normalized_path):
                                    should_exclude = True
                                    print(f"  排除 {file_path} (文件规则: {exclude_pattern})")
                                    break
                        
                        if not should_exclude:
                            all_files.append(file_path)
                            print(f"  包含: {file_path}")
            
            # 去重并排序
            all_files = sorted(list(set(all_files)))
            
            print(f"\n找到 {len(all_files)} 个文件需要合并:")
            
            # 写入文件内容
            for file_path in all_files:
                print(f"  - {file_path}")
                
                try:
                    outf.write("\n" + "=" * 60 + "\n")  
                    outf.write(f"FILE: {file_path}\n")
                    outf.write("=" * 60 + "\n")
                    
                    # 尝试多种编码读取文件
                    encodings_to_try = ['utf-8', 'utf-8-sig', 'gbk', 'cp1252', 'latin1']
                    file_content = None
                    used_encoding = None
                    
                    for encoding in encodings_to_try:
                        try:
                            with open(file_path, 'r', encoding=encoding) as inf:
                                file_content = inf.read()
                                used_encoding = encoding
                            break
                        except UnicodeDecodeError:
                            continue
                        except Exception as e:
                            print(f"    使用编码 {encoding} 读取失败: {e}")
                            continue
                    
                    if file_content is not None:
                        outf.write(file_content)
                        outf.write("\n\n")
                        if used_encoding != 'utf-8':
                            print(f"    使用编码: {used_encoding}")
                    else:
                        error_msg = "[ERROR: 无法使用任何编码读取文件]\n\n"
                        outf.write(error_msg)
                        print(f"    错误: 无法读取文件")
                        
                except Exception as e:
                    error_msg = f"[ERROR 读取文件时出错: {e}]\n\n"
                    outf.write(error_msg)
                    print(f"    读取 {file_path} 时出错: {e}")
            
            # 写入结尾
            outf.write("\n" + "=" * 80 + "\n")
            outf.write("代码文件结束\n")
            outf.write("=" * 80 + "\n")
            
        finally:
            # 恢复原始工作目录
            os.chdir(original_dir)
    
    # 显示结果统计
    file_size_kb = os.path.getsize(output_file) / 1024
    print(f"\n所有文件合并成功!")
    print(f"输出文件: {output_file}")
    print(f"文件大小: {file_size_kb:.2f} KB")
    print(f"包含文件总数: {len(all_files)}")
    
    # 检查是否有遗漏的重要文件
    check_missing_files(base_dir, all_files)

def check_missing_files(base_dir, included_files):
    """检查可能遗漏的重要文件"""
    print(f"\n检查可能遗漏的重要文件...")
    
    # 重要文件列表
    important_files = [
        "Program.cs",
        "appsettings.json",
        "appsettings.Development.json",
        "VacantRoomWeb.csproj",
        "web.config",
        "Services/IEmailService.cs",
        "Services/EmailService.cs",
        "Services/ConfigurationService.cs",
        "Services/StartupLoggingService.cs",
        "Services/FileBasedLoggingService.cs",
        "Components/App.razor",
        "Components/_Imports.razor",
        "Components/Routes.razor"
    ]
    
    missing_files = []
    for important_file in important_files:
        # 标准化路径分隔符
        normalized_important = important_file.replace('/', os.sep)
        
        # 检查是否在包含列表中
        found = False
        for included_file in included_files:
            normalized_included = included_file.replace('\\', os.sep).replace('/', os.sep)
            if normalized_included.endswith(normalized_important):
                found = True
                break
        
        if not found:
            # 检查文件是否实际存在
            full_path = os.path.join(base_dir, normalized_important)
            if os.path.exists(full_path):
                missing_files.append(important_file)
    
    if missing_files:
        print("发现以下重要文件可能被遗漏:")
        for missing_file in missing_files:
            print(f"  - {missing_file}")
        print("请检查文件模式或排除规则。")
    else:
        print("所有重要文件都已包含。")

if __name__ == "__main__":
    try:
        combine_code_files()
        print("\n打包完成! ")
    except Exception as e:
        print(f"错误: {e}")
        print("按回车键退出...")
        input()