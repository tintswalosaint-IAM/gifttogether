from pathlib import Path

html_files = [
    "GiftTogether/wwwroot/index.html",
    "GiftTogether/wwwroot/register.html",
    "GiftTogether/wwwroot/login.html",
    "GiftTogether/wwwroot/dashboard.html",
    "GiftTogether/wwwroot/create-registry.html",
    "GiftTogether/wwwroot/manage-registry.html",
    "GiftTogether/wwwroot/r/index.html",
]

head_block = """
    <link rel="manifest" href="/manifest.json">
    <meta name="theme-color" content="#6c63ff">
    <meta name="apple-mobile-web-app-capable" content="yes">
    <meta name="apple-mobile-web-app-title" content="Neo">
    <meta name="apple-mobile-web-app-status-bar-style" content="default">
    <link rel="apple-touch-icon" href="/icons/icon-192.png">
"""

script_tag = '    <script src="/js/neo.js"></script>\n'

for file in html_files:
    path = Path(file)

    if not path.exists():
        print(f"SKIPPED missing: {file}")
        continue

    text = path.read_text(encoding="utf-8")

    if 'rel="manifest"' not in text:
        text = text.replace("</head>", head_block + "\n</head>")

    if "/js/neo.js" not in text:
        text = text.replace("</body>", script_tag + "</body>")

    path.write_text(text, encoding="utf-8")
    print(f"PATCHED: {file}")

print("Done.")
