using System.Text.Json;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Sql;

class SqlWriter
{
    static readonly Guid ADMIN_ID = new("0199f6b6-7c04-7e7f-9236-1c609d90086c");
    static readonly Guid MEDIA_TYPE_ID_PHOTO = new("01964f94-fa50-7846-b2e6-26d4609cc972");
    static readonly Guid MEDIA_TYPE_ID_VIDEO = new("01964f94-fa51-705b-b0e2-b4c668ac6fab");
    static readonly Guid MEDIA_TYPE_ID_VIDEO_POSTER = new("01964f94-fa51-705b-b0e2-b4c668ac6fcd");
    static readonly DateTime NOW = DateTime.Now;

    public async Task GenerateSql(Category category)
    {
        if (File.Exists(category.SqlFile))
        {
            File.Move(category.SqlFile, $"{category.SqlFile}.old");
        }

        using var sw = new StreamWriter(category.SqlFile);

        await WritePreamble(sw);
        await WriteCategory(sw, category);
        await WriteCategoryRoles(sw, category);
        await WriteLocations(sw, category);
        await WriteMedia(sw, category);
        await WriteCategoryMedia(sw, category);
        await WriteRefreshMaterializedViews(sw);
        await WritePostscript(sw);
    }

    static async Task WriteCategory(StreamWriter writer, Category category)
    {
        await writer.WriteLineAsync(
            $"""
                INSERT INTO media.category (
                    id,
                    name,
                    effective_date,
                    created,
                    created_by,
                    modified,
                    modified_by
                ) VALUES (
                    {SqlAsString(category.Id)},
                    {SqlString(category.Name)},
                    {SqlDate(category.EffectiveDate)},
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)},
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)}
                );

                """
        );
    }

    static async Task WriteCategoryRoles(StreamWriter writer, Category category)
    {
        foreach (var role in category.Roles)
        {
            await writer.WriteLineAsync(
                $"""
                INSERT INTO media.category_role (
                    category_id,
                    role_id,
                    created,
                    created_by
                ) VALUES (
                    {SqlAsString(category.Id)},
                    (SELECT id FROM media.role WHERE name = '{role}'),
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)}
                );

                """);
        }
    }

    static async Task WriteLocations(StreamWriter writer, Category category)
    {
        foreach (var media in category.Media.Where(x => x.Exif?.Latitude != null))
        {
            await writer.WriteLineAsync(
                $"""
                IF NOT EXISTS (
                    SELECT 1
                    FROM media.location
                    WHERE
                        latitude = {SqlGpsCoord(media.Exif!.Latitude)}
                        AND
                        longitude = {SqlGpsCoord(media.Exif.Longitude)}
                )
                THEN
                    INSERT INTO media.location (
                        id,
                        latitude,
                        longitude
                    ) VALUES (
                        {SqlAsString(Guid.CreateVersion7())},
                        {SqlGpsCoord(media.Exif.Latitude)},
                        {SqlGpsCoord(media.Exif.Longitude)}
                    );
                END IF;

                """);
        }
    }

    static async Task WriteMedia(StreamWriter writer, Category category)
    {
        foreach (var media in category.Media)
        {
            var locationId =
                media.Exif?.Latitude == null || media.Exif?.Longitude == null
                    ? "NULL"
                    :   $"""
                            (
                                SELECT id
                                FROM media.location
                                WHERE
                                    latitude = {SqlGpsCoord(media.Exif!.Latitude)}
                                    AND
                                    longitude = {SqlGpsCoord(media.Exif.Longitude)}
                            )
                        """;

            await writer.WriteLineAsync(
                $"""
                INSERT INTO media.media (
                    id,
                    type_id,
                    location_id,
                    location_override_id,
                    created,
                    created_by,
                    modified,
                    modified_by,
                    duration,
                    metadata
                ) VALUES (
                    {SqlAsString(media.Id)},
                    {SqlMediaType(media.MediaType)},
                    {locationId},
                    {SqlString(null)},
                    {SqlDate(media.Exif!.CreateDate)},
                    {SqlAsString(ADMIN_ID)},
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)},
                    {SqlNonString(media.VideoDuration)},
                    {SqlJson(media.Exif?.Json)}
                );

                """);

            await WriteMediaFiles(writer, media);
        }
    }

    static async Task WriteMediaFiles(StreamWriter writer, MediaFile media)
    {
        foreach (var file in media.ScaledFiles)
        {
            await writer.WriteLineAsync(
                $"""
                INSERT INTO media.file (
                    id,
                    media_id,
                    type_id,
                    scale_id,
                    width,
                    height,
                    bytes,
                    path
                ) VALUES (
                    {SqlAsString(file.Id)},
                    {SqlAsString(media.Id)},
                    {SqlFileMediaType(media.MediaType, file.Scale.IsPoster)},
                    (SELECT id FROM media.scale WHERE code = '{file.Scale.Code}'),
                    {SqlNonString(file.Width)},
                    {SqlNonString(file.Height)},
                    {SqlNonString(file.Bytes)},
                    {SqlString(file.Path)}
                );

                """);
        }
    }

    static async Task WriteCategoryMedia(StreamWriter writer, Category category)
    {
        var isTeaser = true;

        foreach (var media in category.Media)
        {
            await writer.WriteLineAsync(
                $"""
                INSERT INTO media.category_media (
                    category_id,
                    media_id,
                    is_teaser,
                    created,
                    created_by,
                    modified,
                    modified_by
                ) VALUES (
                    {SqlAsString(category.Id)},
                    {SqlAsString(media.Id)},
                    {SqlNonString(isTeaser)},
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)},
                    {SqlDate(NOW)},
                    {SqlAsString(ADMIN_ID)}
                );

                """);

            isTeaser = false;
        }
    }

    static async Task WriteRefreshMaterializedViews(StreamWriter writer)
    {
        await writer.WriteLineAsync(
            """
            REFRESH MATERIALIZED VIEW media.category_search;

            """
        );
    }

    static async Task WritePreamble(StreamWriter writer)
    {
        await writer.WriteLineAsync(
            """
            DO
            $$
            BEGIN

            """
        );
    }

    static async Task WritePostscript(StreamWriter writer)
    {
        await writer.WriteLineAsync(
            """
            END
            $$

            """
        );
    }

    static string SqlAsString<T>(T val) => val switch
    {
        null => "NULL",
        _ => $"'{val}'"
    };

    static string SqlString(string? val) => val switch
    {
        null => "NULL",
        "" => "NULL",
        _ => $"'{val.Replace("'", "''")}'"
    };

    static string SqlDate(DateTime? val) => val switch
    {
        null => "NULL",
        _ => $"'{val.Value:yyyy-MM-dd HH:mm:ss}'"
    };

    static string SqlMediaType(MediaType? val) => val switch
    {
        MediaType.Image => SqlAsString(MEDIA_TYPE_ID_PHOTO),
        MediaType.Video => SqlAsString(MEDIA_TYPE_ID_VIDEO),
        _ => "NULL"
    };

    static string SqlFileMediaType(MediaType? val, bool isPoster)
    {
        if (isPoster)
        {
            return SqlAsString(MEDIA_TYPE_ID_VIDEO_POSTER);
        }

        return SqlMediaType(val);
    }

    static string SqlNonString<T>(T val) => val switch
    {
        null => "NULL",
        _ => $"{val}"
    };

    static string SqlGpsCoord(decimal? val) => val switch
    {
        null => "NULL",
        _ => $"{val:F6}"
    };

    static string SqlJson(JsonElement? json) => json switch
    {
        null => "NULL",
        _ => $"'{json?.ToString()?.Replace("'", "''")}'::jsonb"
    };
}
