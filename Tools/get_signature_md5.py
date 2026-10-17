#!/usr/bin/env python
"""
获取 Android 签名证书 MD5（TapTap 开发者中心「配置签名证书」需要填的值）。

用法（三种，按可得性选一种）：
  1) 有 keystore + 知道密码（本项目：Keys/pixeladventure-release.jks）
     python Tools/get_signature_md5.py --keystore Keys/pixeladventure-release.jks
     交互式输入密码（不落盘、不进 shell 历史）

  2) 只有打好的 APK（不用密码）
     python Tools/get_signature_md5.py --apk Builds/xxx.apk

  3) 用 Android Studio
     ./gradlew signingReport  → MD5 值带冒号，填后台前要删掉冒号

⚠️ 填后台格式：**32 位大写十六进制、不带冒号**
   正确：6EB4347CF9C098BE1C8D965D539C42E2
   错误：6E:B4:34:7C:...（带冒号）
"""
import argparse
import hashlib
import re
import subprocess
import sys
from pathlib import Path


def fmt(md5_hex: str) -> str:
    """32 位大写无冒号 —— TapTap 后台要的格式。"""
    return md5_hex.replace(":", "").upper()


def from_keystore(path: Path, alias: str | None) -> str:
    # 找 keytool：优先系统，其次 Unity/团结引擎自带的 OpenJDK
    candidates = []
    which = subprocess.run(["where", "keytool"], capture_output=True, text=True)
    if which.returncode == 0:
        candidates += [Path(p) for p in which.stdout.split() if p.strip()]

    project = Path(__file__).resolve().parent.parent
    candidates += list(project.glob("20*/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe"))
    candidates += list(project.glob("20*/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool"))

    keytool = next((c for c in candidates if c.exists()), None)
    if keytool is None:
        sys.exit("[错误] 找不到 keytool。请安装 JDK 或设置 PATH，或改用 --apk 模式。")

    if alias is None:
        #列出所有别名，交互选择
        import getpass
        pwd = getpass.getpass(f"请输入 {path.name} 的密码（不会显示）：")
        listing = subprocess.run(
            [str(keytool), "-list", "-keystore", str(path), "-storepass", pwd],
            capture_output=True, text=True, encoding="utf-8", errors="replace")
        if listing.returncode != 0:
            sys.exit(f"[错误] 打开 keystore 失败（密码不对？）：\n{listing.stderr}")
        aliases = re.findall(r"^\s{2}([\w\-]+),", listing.stdout, re.M)
        if not aliases:
            sys.exit("[错误] 未在 keystore 中找到别名。")
        print("可用别名：", ", ".join(aliases))
        alias = input("用哪个别名？").strip()
        if alias not in aliases:
            sys.exit("[错误] 别名不存在。")

    import getpass
    pwd = getpass.getpass(f"请输入 {path.name} 的密码（不会显示）：")

    out = Path("_cert_tmp.der")
    try:
        r = subprocess.run(
            [str(keytool), "-exportcert", "-alias", alias,
             "-keystore", str(path), "-storepass", pwd, "-file", str(out)],
            capture_output=True, text=True, encoding="utf-8", errors="replace")
        if r.returncode != 0:
            sys.exit(f"[错误] 导出证书失败：\n{r.stderr}\n（多半是密码或别名不对）")
        return hashlib.md5(out.read_bytes()).hexdigest()
    finally:
        if out.exists():
            out.unlink()


def extract_x509_der(pkcs7: bytes) -> bytes:
    """从 PKCS#7（META-INF/*.RSA）里剥出第一张 X.509 证书的 DER。
    ⚠️ 后台要的是「证书本身」的 MD5；直接 hash CERT.RSA 整块会得到错值（2026-10-17 实锤翻车）。"""
    def tlv(buf, off):
        tag = buf[off]; i = off + 1
        ln = buf[i]; i += 1
        if ln & 0x80:
            n = ln & 0x7F
            ln = int.from_bytes(buf[i:i + n], 'big'); i += n
        return tag, i, i + ln

    t, vs, ve = tlv(pkcs7, 0)
    assert t == 0x30, "ContentInfo 不是 SEQUENCE"
    t, _, e = tlv(pkcs7, vs); assert t == 0x06, "缺 contentType OID"
    t, s, e = tlv(pkcs7, e);  assert t == 0xA0, "缺 [0] content"
    t, s, e = tlv(pkcs7, s);  assert t == 0x30, "缺 SignedData SEQUENCE"
    off = s
    t, _, off = tlv(pkcs7, off); assert t == 0x02, "缺 version"
    t, _, off = tlv(pkcs7, off); assert t == 0x31, "缺 digestAlgorithms"
    t, _, off = tlv(pkcs7, off); assert t == 0x30, "缺 contentInfo"
    t, s, e = tlv(pkcs7, off);   assert t == 0xA0, "SignedData 里没有证书段"
    t, cs, ce = tlv(pkcs7, s)
    assert t == 0x30, "证书不是 SEQUENCE"
    return pkcs7[s:ce]


def from_apk(path: Path) -> str:
    """从 APK 里挖 META-INF/*.RSA(DSA/EC)，剥出证书后算 MD5，无需密码。"""
    import zipfile
    with zipfile.ZipFile(path) as z:
        certs = [n for n in z.namelist()
                 if n.upper().startswith("META-INF/")
                 and n.upper().endswith((".RSA", ".DSA", ".EC"))]
        if not certs:
            sys.exit("[错误] APK 内未找到签名证书（META-INF/*.RSA）。APK 可能是未签名的。")
        print("APK 内证书：", ", ".join(certs))
        return hashlib.md5(extract_x509_der(z.read(certs[0]))).hexdigest()


def main():
    ap = argparse.ArgumentParser(description="获取 Android 签名证书 MD5")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--keystore", type=Path, help="签名文件路径")
    g.add_argument("--apk", type=Path, help="已打好的 APK 路径")
    ap.add_argument("--alias", default=None, help="keystore 别名（不给则交互选择）")
    args = ap.parse_args()

    if args.keystore is not None and not args.keystore.exists():
        sys.exit(f"[错误] 文件不存在：{args.keystore}")
    if args.apk is not None and not args.apk.exists():
        sys.exit(f"[错误] 文件不存在：{args.apk}")

    md5 = from_keystore(args.keystore, args.alias) if args.keystore else from_apk(args.apk)
    md5 = fmt(md5)

    print()
    print("=" * 56)
    print("签名 MD5（填入 TapTap 开发者中心 · 游戏服务 · 应用配置 · 配置签名证书）")
    print(f"  {md5}")
    print()
    print("  格式要求：32 位大写十六进制、不带冒号、不要空格")
    print(f"  校验：长度={len(md5)}，全大写十六进制={bool(re.fullmatch(r'[0-9A-F]{32}', md5))}")
    print("=" * 56)


if __name__ == "__main__":
    main()