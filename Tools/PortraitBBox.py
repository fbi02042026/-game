import pathlib, struct, zlib


def load(path):
    b = pathlib.Path(path).read_bytes()
    w, h = struct.unpack('>II', b[16:24])
    ctype = b[25]
    i = 8
    idat = b''
    plte = b''
    trns = b''
    while i < len(b) - 8:
        ln = struct.unpack('>I', b[i:i + 4])[0]
        typ = b[i + 4:i + 8]
        if typ == b'IDAT':
            idat += b[i + 8:i + 8 + ln]
        elif typ == b'PLTE':
            plte = b[i + 8:i + 8 + ln]
        elif typ == b'tRNS':
            trns = b[i + 8:i + 8 + ln]
        i += 12 + ln
        if typ == b'IEND':
            break
    raw = zlib.decompress(idat)
    bpp = 4 if ctype == 6 else (3 if ctype == 2 else 1)
    stride = w * bpp
    prev = bytearray(stride)
    alphas = []
    pos = 0
    for y in range(h):
        ft = raw[pos]; pos += 1
        line = bytearray(raw[pos:pos + stride]); pos += stride
        if ft == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 0xFF
        elif ft == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 0xFF
        elif ft == 3:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 0xFF
        elif ft == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                c = prev[x - bpp] if x >= bpp else 0
                bb = prev[x]
                pa = abs(bb - c); pb = abs(a - c); pc = abs(a + bb - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (bb if pb <= pc else c)
                line[x] = (line[x] + pr) & 0xFF
        prev = line
        if ctype == 6:
            alphas.append(bytes(line[3::4]))
        else:
            if not trns:
                alphas.append(bytes([255]) * w)
            else:
                alphas.append(bytes(trns[line[x]] if line[x] < len(trns) else 255 for x in range(w)))
    return w, h, alphas


def bbox(w, h, alphas):
    minx, miny, maxx, maxy = w, h, -1, -1
    for y in range(0, h, 2):
        row = alphas[y]
        for x in range(0, w, 2):
            if row[x] > 128:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    return minx, miny, maxx, maxy


root = pathlib.Path(r'Y:\PixelAdventureTown\Assets\Art\UI\Icons\佣兵立绘')
targets = [
    ('新·梅莉莎', r'C:\Users\Administrator\Desktop\梅莉莎.png'),
    ('前台小姐', str(root / '前台小姐.png')),
    ('会长·大众', str(root / '会长——大众.png')),
    ('艾丽娅 C001', str(root / '佣兵立绘_C001.png')),
    ('劳顿 H001', str(root / '佣兵立绘_H001.png')),
    ('索菲 H011', str(root / '佣兵立绘_H011.png')),
]

visible_frac = 0.72
print('%-12s %-13s %-9s %-8s %-8s %-8s' % ('立绘', '尺寸', '头顶留白', '左留白', '右留白', '底边y'))
print('-' * 72)
for name, path in targets:
    if not pathlib.Path(path).exists():
        print('%-12s MISSING' % name)
        continue
    w, h, alphas = load(path)
    minx, miny, maxx, maxy = bbox(w, h, alphas)
    vis = int(h * visible_frac)
    tail = ('超出可见区 %+d px' % (maxy - vis)) if maxy > vis else '全在可见区内'
    print('%-12s %-13s %-9d %-8d %-8d %-8d %s' % (name, '%dx%d' % (w, h), miny, minx, w - 1 - maxx, maxy, tail))
