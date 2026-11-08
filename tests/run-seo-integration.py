"""Windows integration test with a disposable, loopback-only PostgreSQL cluster."""
import concurrent.futures
import json
import os
from pathlib import Path
import re
import socket
import statistics
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from html.parser import HTMLParser

REPO = Path(__file__).resolve().parents[1]
PG_BIN = Path(os.environ.get("SEO_TEST_PG_BIN", r"C:\Program Files\PostgreSQL\16\bin"))


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


class Head(HTMLParser):
    def __init__(self):
        super().__init__()
        self.active = False
        self.canonical = []
        self.description = []
        self.robots = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "head":
            self.active = True
        if not self.active:
            return
        if tag == "link" and attrs.get("rel") == "canonical":
            self.canonical.append(attrs.get("href"))
        if tag == "meta":
            name = attrs.get("name")
            if name == "description":
                self.description.append(attrs.get("content"))
            if name == "robots":
                self.robots.append(attrs.get("content"))

    def handle_endtag(self, tag):
        if tag == "head":
            self.active = False


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def get(base, path, method="GET"):
    try:
        response = urllib.request.build_opener(NoRedirect).open(
            urllib.request.Request(base + path, method=method), timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.read()


def main():
    if not (PG_BIN / "initdb.exe").is_file():
        raise RuntimeError("Set SEO_TEST_PG_BIN to the PostgreSQL bin directory")
    base_dir = Path(tempfile.mkdtemp(prefix="moonphim-seo-integration-"))
    pg_port, http_port = free_port(), free_port()
    pg_started = False
    host_process = None
    report = {"fixture_path": str(base_dir), "checks": []}
    log = (base_dir / "test.log").open("w", encoding="utf-8")
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    try:
        def run(command, timeout=60):
            subprocess.run(command, stdout=log, stderr=log, check=True,
                           timeout=timeout, creationflags=flags)
        run([str(PG_BIN / "initdb.exe"), "-D", str(base_dir / "pgdata"),
             "-U", "seo_fixture", "-A", "trust", "-E", "UTF8", "--locale=C"])
        run([str(PG_BIN / "pg_ctl.exe"), "-D", str(base_dir / "pgdata"),
             "-l", str(base_dir / "postgres.log"), "-o", f"-h 127.0.0.1 -p {pg_port}", "-w", "start"])
        pg_started = True
        host = base_dir / "host"
        host.mkdir()
        project = host / "SeoHost.csproj"
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net9.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="{REPO.as_posix()}/MovieWeb.csproj" /></ItemGroup></Project>')
        source = (REPO / "tests/fixtures/SeoHost.cs.txt").read_text(encoding="utf-8")
        source = source.replace(r'D:\Doan\webnc\MovieWeb', str(REPO))
        (host / "Program.cs").write_text(source, encoding="utf-8")
        if os.environ.get('SEO_TEST_APP_DLL'):
            packages = ''.join(ET.tostring(node, encoding='unicode') for node in ET.parse(REPO / 'MovieWeb.csproj').findall('.//PackageReference'))
            project.write_text(f'<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net9.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include="MovieWeb"><HintPath>{os.environ["SEO_TEST_APP_DLL"]}</HintPath></Reference>{packages}</ItemGroup></Project>')
        run(["dotnet", "build", str(project), "--ignore-failed-sources", "-p:NuGetAudit=false", "-p:WarningLevel=0", "--verbosity", "quiet"], timeout=180)
        host_process = subprocess.Popen(["dotnet", str(host / "bin/Debug/net9.0/SeoHost.dll"), str(pg_port), str(http_port)],
                                        stdout=log, stderr=log, creationflags=flags)
        base = f"http://127.0.0.1:{http_port}"
        for _ in range(120):
            if host_process.poll() is not None:
                raise RuntimeError("Fixture host failed; inspect test.log")
            try:
                if get(base, "/")[0] == 200:
                    break
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.25)
        else:
            raise TimeoutError("Fixture host did not become ready")
        if os.environ.get('SEO_TEST_PERF') == '1':
            timings = {}
            for url in ['/the-loai/phim-le', '/the-loai/phim-bo', '/phim-trending']:
                samples = []
                for _ in range(8):
                    started = time.perf_counter()
                    status, body = get(base, url)
                    assert status == 200, (url, status)
                    samples.append(round((time.perf_counter() - started) * 1000, 2))
                timings[url] = {'first_ms': samples[0], 'warm_median_ms': statistics.median(samples[1:]), 'samples_ms': samples}
            report['performance_fixture'] = timings
            print('PERF_BENCHMARK', json.dumps(timings), flush=True)
        cases = [('/', '/', False), ('/trang-chu', '/trang-chu', False),
                 ('/the-loai/phim-le', '/the-loai/phim-le', False),
                 ('/the-loai/phim-bo?page=2', '/the-loai/phim-bo?page=2', False),
                 ('/the-loai/hoat-hinh', '/the-loai/hoat-hinh', False),
                 ('/the-loai/phim-moi-cap-nhat', '/the-loai/phim-moi-cap-nhat', False),
                 ('/the-loai/rong', '/the-loai/rong', False),
                 ('/quoc-gia/han-quoc', '/quoc-gia/han-quoc', False),
                 ('/the-loai/phim-le?countries=han-quoc', '/the-loai/phim-le?countries=han-quoc', True),
                 ('/phim/seo-phim-1?t=300&utm_source=fb', '/phim/seo-phim-1', False)]
        for url, canonical, noindex in cases:
            status, body = get(base, url)
            assert status == 200, (url, status)
            html = body.decode("utf-8")
            head = Head()
            head.feed(html)
            assert head.canonical == ["https://moonphim.me" + canonical], (url, head.canonical)
            assert len(head.description) == 1 and head.description[0], (url, head.description)
            assert ("noindex, follow" in head.robots) == noindex, (url, head.robots)
            assert len(re.findall(r'<html\b', html)) == 1, url
            for value in re.findall(r'<script[^>]*type="application/ld\+json"[^>]*>(.*?)</script>', html, re.S):
                json.loads(value)
            report["checks"].append({"url": url, "status": status, "metadata": "PASS"})
        status, episode_body = get(base, '/phim/seo-phim-1/tap-1?server=vietsub')
        assert status == 200, status
        episode_html = episode_body.decode('utf-8')
        episode_head = Head()
        episode_head.feed(episode_html)
        assert episode_head.canonical == ['https://moonphim.me/phim/seo-phim-1/tap-1']
        assert 'Tập 1' in episode_html
        assert re.search(r'class="episode-list-item active"', episode_html)
        assert '/phim/seo-phim-1/tap-1?server=vietsub' in episode_html
        assert get(base, '/phim/seo-phim-1/tap-missing')[0] == 404
        assert get(base, '/phim/seo-phim-1/tap-1?server=missing')[0] == 404
        report['episode_routes'] = 'PASS (deep link, active selection, canonical, invalid episode/server)'
        assert get(base, '/phim/does-not-exist')[0] == 404
        assert get(base, '/tim-kiem?keyword=SEO')[0] == 200
        assert b'noindex, follow' in get(base, '/tim-kiem?keyword=SEO')[1]
        assert get(base, '/api/sitemap/clear-cache', method='POST')[0] in {401, 403, 302}
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            responses = list(pool.map(lambda _: get(base, '/sitemap-main.xml'), range(12)))
        assert all(status == 200 for status, body in responses)
        assert len({body for status, body in responses}) == 1
        ns = {'s': 'http://www.sitemaps.org/schemas/sitemap/0.9'}
        # /sitemap.xml là sitemap index; URL trang/phim nằm ở /sitemap-main.xml, URL tập ở /sitemap-episodes-N.xml
        index_status, index_body = get(base, '/sitemap.xml')
        assert index_status == 200
        index = ET.fromstring(index_body)
        assert index.tag.endswith('sitemapindex')
        index_locs = [e.text for e in index.findall('s:sitemap/s:loc', ns)]
        assert 'https://moonphim.me/sitemap-main.xml' in index_locs
        episodes_status, episodes_body = get(base, '/sitemap-episodes-1.xml')
        assert episodes_status in {200, 404}
        if episodes_status == 200:
            episode_urls = [u.text for u in ET.fromstring(episodes_body).findall('s:url/s:loc', ns)]
            assert all('/phim/' in u and '?' not in u for u in episode_urls)
        assert get(base, '/sitemap-episodes-0.xml')[0] == 404
        sitemap = ET.fromstring(responses[0][1])
        assert sitemap.tag.endswith('urlset')
        urls = [u.text for u in sitemap.findall('s:url/s:loc', ns)]
        assert len(urls) == len(set(urls))
        assert 'https://moonphim.me/trang-chu' in urls
        assert 'https://moonphim.me/phim/seo-trailer' in urls
        assert not any('seo-invalid' in u or 'seo-inactive' in u or '/rong' in u or '/tim-kiem' in u for u in urls)
        for entry in sitemap.findall('s:url', ns):
            if '/phim/' not in entry.findtext('s:loc', namespaces=ns):
                assert entry.find('s:lastmod', ns) is None
        report['sitemap'] = {'urls': len(urls), 'parallel_requests': len(responses), 'result': 'PASS'}
        if os.environ.get('SEO_TEST_HTTP_ONLY') != '1':
            run(['node', str(REPO / 'tests/seo-browser-smoke.cjs'), base])
            report['browser'] = 'PASS'
        else:
            report['browser'] = 'SKIPPED (HTTP-only run)'
        report['lastmod_and_episode_id'] = 'PASS (fixture startup assertions)'
        report['result'] = 'PASS'
        (base_dir / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report))
    finally:
        if host_process and host_process.poll() is None:
            host_process.terminate()
            try:
                host_process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                host_process.kill()
                host_process.wait(timeout=10)
        if pg_started:
            subprocess.run([str(PG_BIN / 'pg_ctl.exe'), '-D', str(base_dir / 'pgdata'), '-m', 'fast', '-w', 'stop'],
                           stdout=log, stderr=log, timeout=30, creationflags=flags)
        log.close()
        print('Fixture logs:', base_dir / 'test.log')


if __name__ == '__main__':
    main()
