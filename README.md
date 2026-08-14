# Jellyfin Media Converter

A Jellyfin plugin that lets you browse your library from the dashboard, pick a
video, and convert it to a different container/codec using the server's own
bundled ffmpeg.

It automatically uses whichever hardware transcoder (Intel QSV, Nvidia NVENC,
AMD AMF) is already configured in Jellyfin's playback settings, falling back
to CPU if none is enabled. You can either replace the original file in place
or create a new variant alongside it, and follow the job's progress live from
the dashboard.

## Features

- Library browser built into the dashboard (menu icon: Media Converter)
- Curated container/codec presets, plus an advanced mode for raw ffmpeg args
- Uses the server's already-configured hardware transcoder (QSV/NVENC/AMF) or CPU
- Replace-in-place or create-a-new-variant output modes
- Live conversion progress

## Requirements

- Jellyfin server 12.0.0-rc5 (the plugin is pinned to this exact server version;
  a mismatched server will show the plugin as "NotSupported")
- [.NET SDK 10.0](https://dotnet.microsoft.com/en-us/download/dotnet) to build from source

## Installation

### Via the plugin repository (recommended)

This repo publishes itself as a Jellyfin plugin repository, so the server can
find and auto-update the plugin on its own:

1. In Jellyfin, go to **Dashboard → Plugins → Repositories → Add Repository**.
2. Set the URL to:
   ```
   https://raw.githubusercontent.com/rangoDJ/jellyfin-media-converter/master/manifest.json
   ```
3. Go to **Catalog**, find **Media Converter**, and install it.

New releases (see [Releasing](#releasing)) appear here automatically; Jellyfin
will offer to update to them like any other plugin.

### Manually

Build the plugin (see below), then copy `Jellyfin.Plugin.MediaConverter.dll`
into a `MediaConverter` subfolder of your Jellyfin server's plugin directory
(e.g. `C:\Users\{YourUserName}\AppData\Local\jellyfin\plugins\MediaConverter`
on Windows, or `$HOME/.local/share/jellyfin/plugins/MediaConverter` on Linux),
then restart the server.

## Building

```bash
dotnet build Jellyfin.Plugin.MediaConverter.sln
```

The build output includes `Jellyfin.Plugin.MediaConverter.dll`, which is the
artifact referenced by [build.yaml](build.yaml).

## Configuration

The plugin's default settings (in
[PluginConfiguration.cs](Jellyfin.Plugin.MediaConverter/Configuration/PluginConfiguration.cs))
control what the convert dialog preselects:

| Setting | Default | Description |
| --- | --- | --- |
| `DefaultContainer` | `mkv` | Output container preselected in the convert dialog |
| `DefaultVideoCodec` | `hevc` | Codec family preselected; the actual encoder is resolved at conversion time from the server's configured transcoding backend |
| `DefaultQuality` | `23` | Default quality value (QSV `global_quality` scale, lower is higher quality) |
| `MaxConcurrentJobs` | `1` | Maximum simultaneous conversions |
| `TempFileSuffix` | `.mediaconverter.tmp` | Suffix for the temp file ffmpeg writes before it's swapped into place |
| `VariantSuffixTemplate` | `{name}-{codec}{ext}` | Filename template for "create new variant" mode |

## Notes on the config page's JavaScript

Jellyfin's web client (verified against server 12.0.0's own bundle) renders a
plugin's config page by setting `innerHTML` on a scratch element with the
fetched HTML, then extracting *only* `div[data-role="page"]` from it -
anything outside that div, including a `<script>` placed as its sibling, is
discarded before ever reaching the live DOM. Separately, it only re-executes
`<script>` tags (via jQuery, since `innerHTML`-inserted scripts are inert by
spec) when at least one is found *inside* that extracted div. So
[browser.js](Jellyfin.Plugin.MediaConverter/Web/browser.js) is loaded from a
`<script>` that is nested inside `#mediaConverterPage` itself (see
[browser.html](Jellyfin.Plugin.MediaConverter/Web/browser.html)), not placed
after it - putting it outside that div silently breaks Search/Convert/Cancel
with no console error, even though the server serves the correct content.

As an alternative entry point, [itemdetail.js](Jellyfin.Plugin.MediaConverter/Web/itemdetail.js)
is a standalone script for use with a global script injector plugin (e.g.
[JavaScript Injector](https://github.com/JustAMan/jellyfin-plugin-js-injector)).
It adds a floating **Convert** button directly on movie/episode detail pages
by calling this plugin's API, without going through the config page at all.

## Releasing

Pushing a tag matching `v*.*.*` (e.g. `v1.0.1`) triggers
[.github/workflows/publish.yml](.github/workflows/publish.yml), which:

1. Builds the plugin and packages it into a zip.
2. Publishes that zip to a GitHub Release for the tag.
3. Adds an entry for the new version to [manifest.json](manifest.json) and
   commits it back to `master` — this is the file the repository URL above
   points at, so the update becomes visible to Jellyfin as soon as it's pushed.

```bash
git tag v1.0.1
git push origin v1.0.1
```

Bump `version` in [build.yaml](build.yaml) to match before tagging.

## Debugging

See [.vscode/](.vscode) for a working VS Code debug setup that builds the
plugin, copies it into a local Jellyfin server's plugin directory, and
attaches the debugger on launch.

## Licensing

This project is licensed under the [GPLv3](LICENSE).

Because Jellyfin plugins link against Jellyfin's own GPLv3-licensed binary
NuGet packages, any compiled Jellyfin plugin is itself bound by the GPLv3 —
so distributing this plugin (or a derivative) as a closed-source binary is
not permitted.
