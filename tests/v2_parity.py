"""Ads/SEO parity check between UI A (V1) and UI B (V2) on a running local app.

Usage:  python tests/v2_parity.py [base_url] [path ...]
        (app must run with ASPNETCORE_ENVIRONMENT=Development so the ?ui=v2 cookie is allowed)

For every path it fetches the page as V1 (no cookie) and as V2 (cookie moonphim_ui=v2) and
compares everything ads/SEO depend on. Exit code 1 if anything differs. Pages that have no V2
view yet render V1 in both modes and are reported as "V1 fallback".
"""
import re
import sys
import urllib.error
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1].startswith("http") else "http://localhost:5001"
DEFAULT_PATHS = [
    "/", "/trang-chu", "/the-loai/phim-le", "/the-loai/phim-bo", "/the-loai/hoat-hinh",
    "/the-loai/phim-moi-cap-nhat", "/the-loai/phim-trending", "/quoc-gia/han-quoc",
    "/phim/ong-bean", "/phim/ong-bean/tap-full?server=vietsub-1", "/tim-kiem?q=bean",
    "/about", "/dmca", "/privacy", "/contact", "/mien-tru-trach-nhiem",
    "/Auth/Login", "/Auth/Register", "/Auth/ForgotPassword", "/Auth/ResetPassword?userId=1&token=x",
]
# Pages that exist only in V2 (V1 used a modal): no A/B comparison, B must carry the full ads/SEO baseline.
V2_ONLY_PREFIXES = ("/Auth/",)
V2_ONLY_REQUIRED = ["adsense", "zone_vignette", "zone_onclick", "vignette_src", "monetag_meta", "canonical", "title"]
# Git Bash rewrites "/about" to "C:/Program Files/Git/about"; undo that.
PATHS = [re.sub(r"^[A-Za-z]:/Program Files/Git", "", a) for a in sys.argv[1:] if not a.startswith("http")] or DEFAULT_PATHS

CHECKS = {
    "status": None,  # filled from the response
    "title": r"<title>(.*?)</title>",
    "canonical": r'<link rel="canonical" href="([^"]*)"',
    "robots": r'<meta name="robots" content="([^"]*)"',
    "description": r'<meta name="description" content="([^"]*)"',
    "keywords": r'<meta name="keywords" content="([^"]*)"',
    "author": r'<meta name="author" content="([^"]*)"',
    "monetag_meta": r'<meta name="monetag" content="([^"]*)"',
    "og": r'<meta property="(og:[^"]+)" content="([^"]*)"',
    "twitter": r'<meta name="(twitter:[^"]+)" content="([^"]*)"',
    "jsonld": r'<script type="application/ld\+json">(.*?)</script>',
    "h1": r"<h1[^>]*>(.*?)</h1>",
}
MARKERS = {
    "adsense": "ca-pub-8229451063311920",
    "zone_vignette": "11708850",
    "zone_onclick": "11708858",
    "vignette_src": "n6wxm.com/vignette.min.js",
    "onclick_src": "al5sm.com/tag.min.js",
    "popunder_api": "/api/ads/record-popunder",
}


def fetch(path, v2):
    req = urllib.request.Request(BASE + path, headers={"User-Agent": "Mozilla/5.0 parity-check"})
    if v2:
        req.add_header("Cookie", "moonphim_ui=v2")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")


def norm(s):
    s = re.sub(r"<[^>]+>", "", s)  # h1 may gain/lose inner tags in a redesign
    return re.sub(r"\s+", " ", s).strip()


def extract(status, html):
    out = {"status": status}
    for key, rx in CHECKS.items():
        if rx is None:
            continue
        found = re.findall(rx, html, re.S)
        out[key] = sorted(norm(" ".join(f) if isinstance(f, tuple) else f) for f in found)
    for key, needle in MARKERS.items():
        out[key] = html.count(needle) > 0
    out["_is_v2"] = 'class="v2 ' in html
    return out


failed = False
for path in PATHS:
    a, b = extract(*fetch(path, False)), extract(*fetch(path, True))
    if not b["_is_v2"]:
        print(f"[V1 fallback] {path}  (no V2 view yet)")
        continue
    if path.startswith(V2_ONLY_PREFIXES):
        missing = [k for k in V2_ONLY_REQUIRED if not b[k]]
        if b["status"] != 200:
            missing.append(f"status={b['status']}")
        if missing:
            failed = True
            print(f"[FAIL] {path}  (V2-only) missing: {missing}")
        else:
            print(f"[OK]   {path}  (V2-only, baseline present)")
        continue
    diffs = [k for k in a if not k.startswith("_") and a[k] != b[k]]
    if diffs:
        failed = True
        print(f"[FAIL] {path}")
        for k in diffs:
            print(f"    {k}:\n      A={a[k]}\n      B={b[k]}")
    else:
        print(f"[OK]   {path}")

sys.exit(1 if failed else 0)
