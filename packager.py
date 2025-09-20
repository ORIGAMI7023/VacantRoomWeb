import os
import glob
from pathlib import Path

def combine_code_files():
    # Base directory of your project
    base_dir = r"D:\Programing\C#\VacantRoomWeb\VacantRoomWeb"
    
    # Output text file
    output_file = os.path.join(base_dir, "all_code_files.txt")
    
    # File patterns to include
    file_patterns = [
        "*.cs",           # All C# files
        "*.css",          # All CSS files
        "*.razor",        # All Razor files
        "*.csproj",       # Project files
        "*.json",         # Configuration files
        "Components/**/*.razor",  # Razor components in subfolders
        "Pages/**/*.razor",       # Razor pages in subfolders
        "wwwroot/**/*.css",       # CSS files in wwwroot subdirectories
    ]
    
    # Files to exclude (common build/temp files)
    exclude_patterns = [
        "bin/**",
        "obj/**", 
        ".vs/**",
        "*.user",
        "*.cache",
        "bootstrap"
    ]
    
    print(f"Scanning directory: {base_dir}")
    print(f"Output file: {output_file}")
    
    from datetime import datetime
    current_time = datetime.now().strftime("%Y年%m月%d日 %H:%M")
    
    with open(output_file, 'w', encoding='utf-8') as outf:
        outf.write("=" * 80 + "\n")
        outf.write(f"打包时间: {current_time}\n")
        outf.write("VACANTROOM WEB PROJECT - ALL CODE FILES\n")
        outf.write("=" * 80 + "\n\n")
        
        os.chdir(base_dir)  # Change to project directory
        
        all_files = []

        # Collect all matching files with debugging
        for pattern in file_patterns:
            print(f"Searching for pattern: {pattern}")
            matching_files = glob.glob(pattern, recursive=True)
            print(f"  Found {len(matching_files)} files: {matching_files}")
            
            for file_path in matching_files:
                if os.path.isfile(file_path):
                    # Check if file should be excluded
                    should_exclude = False
                    for exclude_pattern in exclude_patterns:
                        if any(part in file_path.replace('\\', '/') for part in exclude_pattern.split('/')):
                            should_exclude = True
                            print(f"  Excluding {file_path} due to pattern {exclude_pattern}")
                            break
                    
                    if not should_exclude:
                        all_files.append(file_path)
                        print(f"  Including: {file_path}")
        
        # Remove duplicates and sort
        all_files = sorted(list(set(all_files)))
        
        print(f"Found {len(all_files)} files to combine:")
        
        for file_path in all_files:
            print(f"  - {file_path}")
            
            try:
                outf.write("\n" + "=" * 60 + "\n")  
                outf.write(f"FILE: {file_path}\n")
                outf.write("=" * 60 + "\n")
                
                # Try to read with UTF-8, fallback to other encodings if needed
                encodings_to_try = ['utf-8', 'utf-8-sig', 'cp1252', 'latin1']
                file_content = None
                
                for encoding in encodings_to_try:
                    try:
                        with open(file_path, 'r', encoding=encoding) as inf:
                            file_content = inf.read()
                        break
                    except UnicodeDecodeError:
                        continue
                
                if file_content is not None:
                    outf.write(file_content)
                    outf.write("\n\n")
                else:
                    outf.write("[ERROR: Could not read file with any encoding]\n\n")
                    
            except Exception as e:
                outf.write(f"[ERROR reading file: {e}]\n\n")
                print(f"    Error reading {file_path}: {e}")
        
        outf.write("\n" + "=" * 80 + "\n")
        outf.write("END OF CODE FILES\n")
        outf.write("=" * 80 + "\n")
    
    print(f"\nAll files combined successfully!")
    print(f"Output file: {output_file}")
    print(f"File size: {os.path.getsize(output_file) / 1024:.2f} KB")
    print(f"Total files included: {len(all_files)}")

if __name__ == "__main__":
    try:
        combine_code_files()
    except Exception as e:
        print(f"Error: {e}")
        input("Press Enter to exit...")