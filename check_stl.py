import struct
import glob
import os

def check_stl(file_path):
    with open(file_path, 'rb') as f:
        f.read(80)
        tri_count = struct.unpack('<I', f.read(4))[0]
        min_x, min_y, min_z = float('inf'), float('inf'), float('inf')
        max_x, max_y, max_z = float('-inf'), float('-inf'), float('-inf')
        
        for _ in range(tri_count):
            try:
                f.read(12) # skip normal
                for _ in range(3):
                    x, y, z = struct.unpack('<fff', f.read(12))
                    min_x = min(min_x, x)
                    min_y = min(min_y, y)
                    min_z = min(min_z, z)
                    max_x = max(max_x, x)
                    max_y = max(max_y, y)
                    max_z = max(max_z, z)
                f.read(2) # skip attr
            except:
                break
        print(f"{os.path.basename(file_path)}: Min({min_x:.1f}, {min_y:.1f}, {min_z:.1f}) Max({max_x:.1f}, {max_y:.1f}, {max_z:.1f})")

for f in sorted(glob.glob('src/RoKiSim_Desktop/Models/*.stl')):
    check_stl(f)
