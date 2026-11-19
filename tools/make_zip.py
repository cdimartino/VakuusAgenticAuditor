"""Write exactly the requested release members, preserving installer permissions."""

import sys
from zipfile import ZIP_DEFLATED, ZipFile

if __name__ == "__main__":
    destination, *members = sys.argv[1:]
    with ZipFile(destination, "w", compression=ZIP_DEFLATED) as archive:
        for member in members:
            archive.write(member, member)
