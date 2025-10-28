using System.Runtime.InteropServices;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Sql;

class ScriptWriter
{
    public async Task WriteRunnerScript(Category category)
    {
        if (File.Exists(category.ScriptFile))
        {
            File.Move(category.ScriptFile, $"{category.ScriptFile}.old");
        }

        StreamWriter? writer = null;

        try
        {
            writer = new StreamWriter(category.ScriptFile);

            await writer.WriteLineAsync(
                """
                #!/bin/bash

                if [ "$1" = "dev" ]; then
                    POD=dev-media-pod
                    ENVPGDATA=~/maw-media/dev/pg-secrets
                else
                    POD=pod-maw-media
                    ENVPGDATA=~/maw-media/pg-secrets
                fi

                """);

            await WriteRunScript(writer, category.SqlFile);
        }
        finally
        {
            if (writer != null)
            {
                await writer.FlushAsync();
                writer.Close();
                await writer.DisposeAsync();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var currMode = File.GetUnixFileMode(category.ScriptFile);
                    var newMode = currMode | UnixFileMode.UserExecute;

                    File.SetUnixFileMode(category.ScriptFile, newMode);
                }
            }
        }
    }

    async Task WriteRunScript(StreamWriter writer, string filename)
    {
        var containerDir = "/scripts";
        var file = Path.Combine(containerDir, Path.GetFileName(filename)!);

        await writer.WriteLineAsync(
            $$"""
            podman run --rm \
                --pod "${POD}" \
                --name pg_import \
                --security-opt label=disable \
                --env "POSTGRES_PASSWORD_FILE=/secrets/psql-postgres" \
                --volume "./:{{containerDir}}" \
                --volume "${ENVPGDATA}:/secrets" \
                docker.io/library/postgres:18-trixie \
                    psql \
                    --host localhost \
                    --port 5432 \
                    --username postgres \
                    --dbname maw_media \
                    --file "{{file}}"
            """);

        await writer.WriteLineAsync();
    }
}
