using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Cuadre.Api.Tests.Integration.Catalog;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Cuadre.Api.Tests.Integration.Backups;

// The pendrive is a folder inside the test's temp directory (Factory.PendriveDirectory).
// The clock says 2026-11-12 15:00 in UTC, so the first manual backup is cuadre-2026-11-12_150000-manual.zip.

[Collection(nameof(ApiCollection))]
public class BackupEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<BackupResultResponse> BackUpAsync()
    {
        var response = await Client.PostAsync(ApiEndpoints.Backups.Create, null, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<BackupResultResponse>(Ct))!;
    }

    private Task<HttpResponseMessage> SetPendriveAsync(string? folder, int alertAfterDays = 7) =>
        Client.PutAsJsonAsync(ApiEndpoints.Backups.Settings,
            new UpdateBackupSettingsRequest { ExternalDirectory = folder, AlertAfterDays = alertAfterDays }, Ct);

    private async Task<BackupStatusResponse> StatusAsync() =>
        (await Client.GetFromJsonAsync<BackupStatusResponse>(ApiEndpoints.Backups.Status, Ct))!;

    private Task<HttpResponseMessage> RestoreAsync(string fileName,
        BackupLocationCode location = BackupLocationCode.Local) =>
        Client.PostAsJsonAsync(ApiEndpoints.Backups.Restore,
            new RestoreBackupRequest { Location = location, FileName = fileName }, Ct);

    private string LocalFile(string fileName) => Path.Combine(Factory.BackupsDirectory, fileName);

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Opens a backup, lets the test change its database and/or its manifest, and zips it back.</summary>
    private static void Tamper(string zipFile, Action<string>? changeDatabase, Action<JsonObject> changeManifest)
    {
        var work = Path.Combine(Path.GetDirectoryName(zipFile)!, $"tamper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            ZipFile.ExtractToDirectory(zipFile, work);
            var database = Path.Combine(work, "cuadre.db");
            var manifestFile = Path.Combine(work, "manifest.json");

            changeDatabase?.Invoke(database);
            SqliteConnection.ClearAllPools();

            var manifest = JsonNode.Parse(File.ReadAllText(manifestFile))!.AsObject();
            manifest["DatabaseSha256"] = Sha256(File.ReadAllBytes(database));
            changeManifest(manifest);
            File.WriteAllText(manifestFile, manifest.ToJsonString());

            File.Delete(zipFile);
            ZipFile.CreateFromDirectory(work, zipFile);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    // ---------- Status ----------

    [Fact]
    public async Task Status_ShouldAskForAPendrive_OnAFreshInstall()
    {
        var status = await StatusAsync();

        status.ExternalDirectory.Should().BeNull();
        status.AlertAfterDays.Should().Be(7);
        status.ExternalAvailable.Should().BeFalse();
        status.LastLocal.Should().BeNull();
        status.Backups.Should().BeEmpty();
        status.Alert!.Kind.Should().Be(BackupAlertCode.NotConfigured);
    }

    [Fact]
    public async Task Status_ShouldWarn_WhenTheLastPendriveCopyIsOlderThanTheDaysChosen()
    {
        (await SetPendriveAsync(Factory.PendriveDirectory)).StatusCode.Should().Be(HttpStatusCode.OK);
        await BackUpAsync();

        Clock.Now = Clock.Now.AddDays(8);
        var status = await StatusAsync();

        status.Alert.Should().BeEquivalentTo(new BackupAlertResponse
        {
            Kind = BackupAlertCode.Overdue,
            DaysSinceExternal = 8
        });
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_ShouldWriteAVerifiedZip_InThePcFolder()
    {
        var result = await BackUpAsync();

        result.Backup.FileName.Should().Be("cuadre-2026-11-12_150000-manual.zip");
        result.Backup.Location.Should().Be(BackupLocationCode.Local);
        result.Backup.Reason.Should().Be(BackupReasonCode.Manual);
        result.Backup.CreatedAt.Should().Be(new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc));
        result.External.Should().Be(ExternalCopyCode.NotConfigured);

        using var zip = ZipFile.OpenRead(LocalFile(result.Backup.FileName));
        zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo("cuadre.db", "manifest.json");

        using var databaseStream = new MemoryStream();
        using (var entry = zip.GetEntry("cuadre.db")!.Open())
        {
            await entry.CopyToAsync(databaseStream, Ct);
        }

        JsonObject manifest;
        using (var entry = zip.GetEntry("manifest.json")!.Open())
        {
            manifest = JsonNode.Parse(entry)!.AsObject();
        }

        manifest["App"]!.GetValue<string>().Should().Be("Cuadre");
        manifest["Reason"]!.GetValue<string>().Should().Be("Manual");
        manifest["SchemaVersion"]!.GetValue<string>().Should().EndWith(".sql");
        manifest["DatabaseSha256"]!.GetValue<string>().Should().Be(Sha256(databaseStream.ToArray()));

        var status = await StatusAsync();
        status.LastLocal!.FileName.Should().Be(result.Backup.FileName);
        status.Backups.Should().ContainSingle(b => b.FileName == result.Backup.FileName);
    }

    [Fact]
    public async Task Create_ShouldNumberASecondBackupInTheSameSecond()
    {
        var first = await BackUpAsync();

        var second = await BackUpAsync();

        // The rotation keeps the newest of the day, and the newest is "-2" (not "manual.zip", as text would say).
        second.Backup.FileName.Should().Be("cuadre-2026-11-12_150000-manual-2.zip");
        File.Exists(LocalFile(second.Backup.FileName)).Should().BeTrue();
        (await StatusAsync()).LastLocal!.FileName.Should().Be(second.Backup.FileName);
        File.Exists(LocalFile(first.Backup.FileName)).Should().BeFalse("same day: only the newest is kept");
    }

    [Fact]
    public async Task Create_ShouldCopyToThePendrive_WhenItIsPluggedIn()
    {
        (await SetPendriveAsync(Factory.PendriveDirectory)).StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await BackUpAsync();

        result.External.Should().Be(ExternalCopyCode.Copied);
        var copy = Path.Combine(Factory.PendriveDirectory, result.Backup.FileName);
        File.Exists(copy).Should().BeTrue();
        Sha256(await File.ReadAllBytesAsync(copy, Ct))
            .Should().Be(Sha256(await File.ReadAllBytesAsync(LocalFile(result.Backup.FileName), Ct)));

        var status = await StatusAsync();
        status.ExternalAvailable.Should().BeTrue();
        status.LastExternal!.FileName.Should().Be(result.Backup.FileName);
        status.Alert.Should().BeNull();
        status.Backups.Select(b => b.Location)
            .Should().BeEquivalentTo([BackupLocationCode.Local, BackupLocationCode.External]);
    }

    [Fact]
    public async Task Create_ShouldStillBackUpInThePc_WhenThePendriveIsNotPluggedIn()
    {
        (await SetPendriveAsync(Factory.PendriveDirectory)).StatusCode.Should().Be(HttpStatusCode.OK);
        Directory.Delete(Factory.PendriveDirectory, recursive: true);   // pulled out

        var result = await BackUpAsync();

        result.External.Should().Be(ExternalCopyCode.Unavailable);
        result.ExternalMessage.Should().Contain("pendrive");
        File.Exists(LocalFile(result.Backup.FileName)).Should().BeTrue();

        // Not plugged in is not a failure: the alert only says nothing reached the pendrive yet.
        var status = await StatusAsync();
        status.ExternalAvailable.Should().BeFalse();
        status.LastProblem.Should().BeNull();
        status.Alert!.Kind.Should().Be(BackupAlertCode.NeverCopied);
    }

    // ---------- Settings ----------

    [Fact]
    public async Task UpdateSettings_ShouldCreateTheFolder_AndSaveIt()
    {
        var response = await SetPendriveAsync(Factory.PendriveDirectory, alertAfterDays: 3);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = (await response.Content.ReadFromJsonAsync<BackupStatusResponse>(Ct))!;
        status.ExternalDirectory.Should().Be(Factory.PendriveDirectory);
        status.AlertAfterDays.Should().Be(3);
        status.ExternalAvailable.Should().BeTrue();
        Directory.Exists(Factory.PendriveDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettings_ShouldStartTheAlertOver_WhenThePendriveChanges()
    {
        await SetPendriveAsync(Factory.PendriveDirectory);
        await BackUpAsync();

        // Inside the first one only so the reset cleans it up.
        var response = await SetPendriveAsync(Path.Combine(Factory.PendriveDirectory, "otro-pendrive"));

        var status = (await response.Content.ReadFromJsonAsync<BackupStatusResponse>(Ct))!;
        status.LastExternal.Should().BeNull();
        status.Alert!.Kind.Should().Be(BackupAlertCode.NeverCopied);
    }

    [Fact]
    public async Task UpdateSettings_ShouldFail_WhenTheDriveIsNotThere()
    {
        var missing = Enumerable.Range('F', 21).Select(c => $"{(char)c}:\\").Last(root => !Directory.Exists(root));

        var response = await SetPendriveAsync(Path.Combine(missing, "Cuadre respaldos"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await StatusAsync()).ExternalDirectory.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_ShouldRefuseThePcBackupsFolder()
    {
        var response = await SetPendriveAsync(Factory.BackupsDirectory);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("respaldos", 7)]
    [InlineData(null, 0)]
    [InlineData(null, 61)]
    public async Task UpdateSettings_ShouldReturnBadRequest_WhenInvalid(string? folder, int days)
    {
        var response = await SetPendriveAsync(folder, days);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSettings_ShouldAllowNoPendrive()
    {
        await SetPendriveAsync(Factory.PendriveDirectory);

        var response = await SetPendriveAsync(null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BackupStatusResponse>(Ct))!.Alert!.Kind
            .Should().Be(BackupAlertCode.NotConfigured);
    }

    // ---------- Restore ----------

    [Fact]
    public async Task Restore_ShouldBringBackTheData_AndKeepACopyOfWhatWasThere()
    {
        var before = await Client.CreateCategoryAsync("Granos", Ct);
        var backup = await BackUpAsync();
        Clock.Now = Clock.Now.AddMinutes(10);
        var after = await Client.CreateCategoryAsync("Bebidas", Ct);

        var response = await RestoreAsync(backup.Backup.FileName);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var result = (await response.Content.ReadFromJsonAsync<RestoreResultResponse>(Ct))!;
        result.Restored.FileName.Should().Be(backup.Backup.FileName);
        result.RestoredCreatedAt.Should().Be(new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc));
        result.SafetyCopy.FileName.Should().Be("cuadre-2026-11-12_151000-antes-de-restaurar.zip");
        result.SafetyCopy.Reason.Should().Be(BackupReasonCode.BeforeRestore);

        (await Client.GetAsync(Route(ApiEndpoints.Categories.Get, before.Id), Ct)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync(Route(ApiEndpoints.Categories.Get, after.Id), Ct)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // Regrets: the safety copy brings "Bebidas" back.
        (await RestoreAsync(result.SafetyCopy.FileName)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync(Route(ApiEndpoints.Categories.Get, after.Id), Ct)).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restore_ShouldKeepTheBackupHistory_BecauseItLivesOutsideTheDatabase()
    {
        await SetPendriveAsync(Factory.PendriveDirectory);
        var backup = await BackUpAsync();

        await RestoreAsync(backup.Backup.FileName);

        var status = await StatusAsync();
        status.ExternalDirectory.Should().Be(Factory.PendriveDirectory);
        status.LastExternal!.FileName.Should().Be(backup.Backup.FileName);
    }

    [Fact]
    public async Task Restore_ShouldRefuseADamagedBackup_AndTouchNothing()
    {
        var backup = await BackUpAsync();
        var category = await Client.CreateCategoryAsync("Bebidas", Ct);
        Tamper(LocalFile(backup.Backup.FileName), changeDatabase: null,
            manifest => manifest["DatabaseSha256"] = new string('0', 64));

        var response = await RestoreAsync(backup.Backup.FileName);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Client.GetAsync(Route(ApiEndpoints.Categories.Get, category.Id), Ct)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await StatusAsync()).Backups.Should().NotContain(b => b.Reason == BackupReasonCode.BeforeRestore);
    }

    [Fact]
    public async Task Restore_ShouldRefuseABackupFromANewerVersion()
    {
        var backup = await BackUpAsync();
        Tamper(LocalFile(backup.Backup.FileName), database =>
        {
            using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO SchemaVersions (ScriptName, Applied) VALUES ('zzz-a-future-migration.sql', '2027-01-01');";
            command.ExecuteNonQuery();
        }, manifest => manifest["SchemaVersion"] = "zzz-a-future-migration.sql");

        var response = await RestoreAsync(backup.Backup.FileName);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title.Should().Contain("más nueva");
    }

    [Theory]
    [InlineData("cuadre-2020-01-01_000000-manual.zip")]
    [InlineData(@"..\data\cuadre-tests.db")]
    public async Task Restore_ShouldReturnNotFound_ForAFileThatIsNotABackupThere(string fileName)
    {
        var response = await RestoreAsync(fileName);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Drives ----------

    [Fact]
    public async Task Drives_ShouldAnswer_EvenWithNoPendrivePluggedIn()
    {
        var response = await Client.GetAsync(ApiEndpoints.Backups.Drives, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RemovableDrivesResponse>(Ct))!.Items.Should().NotBeNull();
    }
}
