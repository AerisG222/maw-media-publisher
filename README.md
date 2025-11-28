# MawMediaPublisher

Tool to prepare and publish photos and videos for media.mikeandwan.us.

## Dependencies

Currently this tool is planned to run on my Linux desktop and no plans to run this elsewhere.
Given that, this tool expects a number of supporting applications to be already installed:

- bash
- exiftool
- ffmpeg
- ffprobe
- magick
- mv
- rawtherapee-cli
- rsync
- xdg-open

## Sample

The following is an example to run this from the project directory during testing:
`dotnet run -- -m ~/maw-media-publisher/ -c "test category's name" -i`

This is an example of how to run for production
dotnet run -- -m ~/maw-media-publisher/ -c "test category's name" -i

dotnet ~/git/maw-media-publisher/src/MawMediaPublisher/bin/Release/net10.0/MawMediaPublisher.dll \
    -m ~/my-photos \
    -c "cat name" \
    -i

## Suggestion

To simplify executing this, create the following alias:

```bash
alias mmp='dotnet ~/git/maw-media-publisher/src/MawMediaPublisher/bin/Release/net10.0/MawMediaPublisher.dll'
```
