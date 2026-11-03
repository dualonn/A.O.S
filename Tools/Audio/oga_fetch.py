"""Download the files of OpenGameArt pages, only if the page is CC0 (or CC-BY when --allow-by).
Usage: python oga_fetch.py <dest_dir> <page-slug-or-url> [...] [--allow-by]
Writes <dest_dir>/<slug>/ with the files plus a SOURCE.txt (url, title, author, license)."""
import os, re, sys, html, urllib.request, urllib.parse

UA = {'User-Agent': 'Mozilla/5.0'}


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60).read()


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    allow_by = '--allow-by' in sys.argv
    dest = args[0]
    for page in args[1:]:
        url = page if page.startswith('http') else 'https://opengameart.org/content/' + page
        text = get(url).decode('utf8', 'replace')
        title = html.unescape(re.search(r'<title>(.*?)</title>', text, re.S).group(1)).split('|')[0].strip()
        lic_block = text[text.find('License(s)'):text.find('License(s)') + 3000]
        lics = sorted(set(re.findall(r'(CC0|CC-BY-SA \d\.\d|CC-BY \d\.\d|GPL \d\.\d|OGA-BY \d\.\d)', lic_block)))
        author = re.search(r'Author:.*?<a[^>]*>(.*?)</a>', text, re.S)
        author = html.unescape(author.group(1)) if author else '?'
        ok = 'CC0' in lics or (allow_by and any(l.startswith(('CC-BY ', 'OGA-BY')) for l in lics))
        files = sorted(set(re.findall(r'https://opengameart\.org/sites/default/files/(?!styles/|audio_preview/)[^"\'<> ]+\.(?:zip|ogg|wav|mp3|flac|rar|7z)', text)))
        print(f'{title!r} by {author} {lics} files={len(files)} {"OK" if ok else "SKIP"}')
        if not ok:
            continue
        slug = re.sub(r'[^a-z0-9]+', '-', url.rstrip('/').split('/')[-1].lower())
        d = os.path.join(dest, slug)
        os.makedirs(d, exist_ok=True)
        with open(os.path.join(d, 'SOURCE.txt'), 'w', encoding='utf8') as f:
            f.write(f'{title}\n{author}\n{" / ".join(lics)}\n{url}\n')
        for fu in files:
            name = urllib.parse.unquote(fu.split('/')[-1])
            p = os.path.join(d, name)
            if not os.path.exists(p):
                data = get(fu)
                open(p, 'wb').write(data)
                print('   ', name, len(data) // 1024, 'KB')


if __name__ == '__main__':
    main()
