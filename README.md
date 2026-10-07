# DeezNET
[![Version](https://img.shields.io/nuget/v/DeezNET.svg)](https://nuget.org/packages/DeezNET)

A .NET Deezer API wrapper and track downloading library. There's a CLI tool in there as well.

## About this fork
This is a fork of [TrevTV/DeezNET](https://github.com/TrevTV/DeezNET). The upstream repository's last change was in May 2025, and its maintainer isn't merging pull requests. This fork keeps the library working for [Lidarr.Plugin.Deezer](https://github.com/jasonpatrickellykrause/Lidarr.Plugin.Deezer), which builds it from source as a submodule.

The NuGet badge above and the package on NuGet are TrevTV's release (1.2.1). This fork isn't published to NuGet. Reference it as a project or submodule instead.

The main differences from upstream:
- Decrypted tracks no longer end with leftover bytes from the previous block.
- The library retries a failed session or expired ARL once, then reports it, instead of retrying without limit.
- Download failures report Deezer's error code instead of writing an error page to disk as audio.
- The library can tag tracks with MusicBrainz IDs.

## Changelog

### 1.2.3
**Fixes**
- `DecodeTrackStream` wrote the whole 6144-byte buffer on every pass, so most tracks ended with up to 6143 stale bytes. It now writes only the bytes it read and keeps the stripe alignment when the stream returns short reads. From [TrevTV/DeezNET#4](https://github.com/TrevTV/DeezNET/pull/4).
- When Deezer rejected the session token, `GWApi` refreshed it and retried with no limit, and refreshing from inside `deezer.getUserData` recursed. It now retries once after a short delay, never refreshes from inside `deezer.getUserData`, and throws `InvalidARLException` if the retry fails.
- A media URL request that returns errors refreshes the license token once, then throws `APIException` with Deezer's error code.
- `NoSourcesAvailableException` includes Deezer's per-track error code and message, which explains rights and region restrictions.
- A non-success response from the CDN throws instead of the library decrypting it and saving it as audio. `WriteRawTrackToFile` creates the output file only after the download starts successfully.
- `SetARL` returns early when given a blank ARL instead of setting it anyway.

**New**
- MusicBrainz ID tagging: `ApplyMetadataToFile` accepts a `MusicBrainzIds` value and writes release, release group, artist, release artist, and recording IDs. From [TrevTV/DeezNET#3](https://github.com/TrevTV/DeezNET/pull/3) by jtstothard.
- `MusicBrainzIds.RecordingId` sets the recording ID for the current track. It takes precedence over `TrackRecordingIds`, which uses per-disc track numbers as keys and collides on multi-disc releases.
- The blind MusicBrainz search runs only when the caller passes no `MusicBrainzIds` value. Pass an empty one to opt out.
- Requests send a browser User-Agent.

**Maintenance**
- Dependabot opens weekly NuGet update pull requests.
- Builds fail on high or critical NuGet advisories, including transitive packages.

## Dear Deezer
If you would like this repository to be taken down, please send me a cease and desist.<br>
You may e-mail it to me here: [me@trev.app](mailto:me@trev.app).

Or go through the standard GitHub DMCA procedure, but that isn't as fun.

## Dependencies
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) for every API call
- [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography) for decrypting track data (`client.Downloader.GetRawTrackBytes()`)
- [TagLibSharp](https://www.nuget.org/packages/TagLibSharp) for applying metadata to decrypted track data (`client.Downloader.ApplyMetadataToTrackBytes()`)

## Overview
DeezNET is built around a core class, `DeezerClient`. That class itself does very little, under it is `Downloader`, `GWApi`, and `PublicApi`.
- `Downloader` provides functions for downloading tracks by their ID, as well as applying metadata. It requires an ARL supplied to `DeezerClient` to function.
- `GWApi` is a wrapper for the backend API used by Deezer. It requires an ARL supplied to `DeezerClient` to function.
- `PublicApi` is a wrapper for the public Deezer API. It does not require an ARL.

All API calls return a Newtonsoft.JSON JToken as I did not want to deal with parsing everything into model classes since there are many different API endpoints.

In addition to `DeezerClient`, there is `DeezerURL` which is a class for parsing Deezer URLs into their entity type and ID. It also handles unshortening the standard Deezer share URLs (deezer.page.link).

## Examples

### Getting Track Info (`PublicApi`)
```cs
var client = new DeezerClient();
var trackData = await client.PublicApi.GetTrack(1903638027);
Console.WriteLine($"{trackData["title"]!} by {trackData["contributors"]!.First()["name"]!}");
// Output: Let You Down by Dawid Podsiadło
```

### Getting Track Info (`GWApi`)
```cs
var client = new DeezerClient();
await client.SetARL("[ARL]");
var trackData = await client.GWApi.GetTrack(1903638027);
Console.WriteLine($"{trackData["SNG_TITLE"]!} by {trackData["ART_NAME"]!}");
// Output: Let You Down by Dawid Podsiadło
```

### Downloading a Track by ID
```cs
var client = new DeezerClient();
await client.SetARL("[ARL]");
var trackBytes = await client.Downloader.GetRawTrackBytes(1903638027, DeezNET.Data.Bitrate.FLAC);
trackBytes = await client.Downloader.ApplyMetadataToTrackBytes(1903638027, trackBytes); // if you want metadata
File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, "LYD.flac"), trackBytes);
// Saves a metadata-applied FLAC of Let You Down by Dawid Podsiadło to your current working directory
```

### Downloading an Album by URL
```cs
var client = new DeezerClient();
await client.SetARL("[ARL]");
var urlData = DeezerURL.Parse("https://deezer.page.link/uwdUFsjkJbGkngSm7"); // this is a short URL, can also be a full one like "https://www.deezer.com/us/album/548556802"
var tracksInAlbum = await urlData.GetAssociatedTracks(client);

foreach (var track in tracksInAlbum)
{
    var trackBytes = await client.Downloader.GetRawTrackBytes(track, DeezNET.Data.Bitrate.FLAC);
    trackBytes = await client.Downloader.ApplyMetadataToTrackBytes(track, trackBytes); // if you want metadata
    File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, $"{track}.flac"), trackBytes);
}
// Saves metadata-applied FLACs of every track in GLOOM DIVISION by I DONT KNOW HOW BUT THEY FOUND ME to your current working directory
```

## DeezCLI
```
USAGE
  DeezCLI <url> [options]

DESCRIPTION
  Downloads the given URL.

PARAMETERS
* url               The URL of the item to download. Can be a shortened URL.

OPTIONS
  -b|--bitrate      The preferred bitrate when downloading. Falls back to a lower quality if unavailable. Choices: "MP3_128", "MP3_320", "FLAC". Default: "FLAC".
  -m|--add-metadata  Whether to attach metadata to the downloaded audio file. Default: "True".
  -L|--add-sync-lyrics  Specifies whether to add a synced lyrics file to the download directory. Default: "False".
  -B|--add-lrclib-lyrics  Specifies whether to fetch lyrics from LRCLIB if available. Default: "False".
  -o|--output       The directory to save downloaded media to. Default: "E:\Projects\GitHub\DeezNET\DeezCLI\bin\Release\net8.0".
  -a|--arl          The account ARL to download with. A paid plan allows for higher quality downloads. Some regions do not have full tracks available without a premium account. Default: "".
  -l|--top-limit    The max amount of tracks to download. Only applicable when downloading an artist's top tracks. Default: "100".
  -c|--concurrent   The max amount of allowed concurrent track downloads. Default: "3".
  -d|--folder-template  The folder path template to use when saving tracks to file. Default: "%albumartist%/%album%/".
  -f|--file-template  The file path template to use when saving tracks to file. Default: "%track% - %title%.%ext%".
  -h|--help         Shows help text.
  --version         Shows version information.
```

```
AVAILABLE TEMPLATE VARIABLES
  %title%
  %album%
  %albumartist%
  %artist%
  %albumartists%
  %artists%
  %track%
  %trackcount%
  %trackid%
  %albumid%
  %artistid%
  %ext%
  %year%
```

## Credits
- This project is heavily based on [deemix-py](https://gitlab.com/RemixDev/deemix-py) and [deezer-py](https://gitlab.com/RemixDev/deezer-py), both under the [GNU GPLv3 License](https://gitlab.com/RemixDev/deemix-py/-/blob/main/LICENSE.txt).
