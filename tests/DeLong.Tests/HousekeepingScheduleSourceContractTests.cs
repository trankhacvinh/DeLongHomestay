using Xunit;

namespace DeLong.Tests;

public sealed class HousekeepingScheduleSourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Schedule_defaults_to_all_tasks_and_text_mode_restores_complete_schedule()
    {
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-housekeeping.js");
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Housekeeping/Index.cshtml");

        Assert.Contains("taskFilter: 'all'", script, StringComparison.Ordinal);
        Assert.Contains("openTextMode()", script, StringComparison.Ordinal);
        Assert.Contains("this.mode = 'text';", script, StringComparison.Ordinal);
        Assert.Contains("this.taskFilter = 'all';", script, StringComparison.Ordinal);
        Assert.Contains("v-on:click=\"openTextMode\"", page, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate repository root from the test output directory.");

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
