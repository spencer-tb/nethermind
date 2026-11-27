// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Nethermind.State.Test.Runner.Test;

/// <summary>
/// <c>nethtest buildtest</c> builds each payload an engine fixture expects to be valid through
/// <c>testing_buildBlockV1</c> and compares it with the fixture's before importing the fixture's payload.
/// </summary>
[TestFixture]
public class BuildTestTests
{
    private const string ValidFixture = "buildtest_valid.json";

    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [TestCase(ValidFixture, 1)]
    // The block's blob transaction comes without its network wrapper, as payloads carry it, and is still built.
    [TestCase("buildtest_blob.json", 1)]
    // The fixture's only payload is expected to be invalid, so nothing is built.
    [TestCase("bal_invalid_surplus_system_address_enginetest.json", 0)]
    public async Task Builds_each_payload_expected_to_be_valid(string fixture, int built)
    {
        JsonElement result = await RunBuildTest(Fixture(fixture));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GetProperty("pass").GetBoolean(), Is.True, result.ToString());
            Assert.That(result.GetProperty("built").GetInt32(), Is.EqualTo(built));
            Assert.That(result.GetProperty("mismatches").GetArrayLength(), Is.Zero);
            Assert.That(result.GetProperty("rejections").GetArrayLength(), Is.Zero);
        }
    }

    [Test]
    public async Task Reports_a_field_in_which_the_built_payload_differs()
    {
        const string wrongRoot = "0x0000000000000000000000000000000000000000000000000000000000000001";
        JsonNode fixtures = JsonNode.Parse(File.ReadAllText(Fixture(ValidFixture)))!;
        JsonNode payload = fixtures.AsObject().Single().Value!["engineNewPayloads"]![0]!["params"]![0]!;
        string builtRoot = payload["stateRoot"]!.GetValue<string>();
        payload["stateRoot"] = wrongRoot;
        string path = Path.Combine(_directory, "mismatch.json");
        File.WriteAllText(path, fixtures.ToJsonString());

        JsonElement result = await RunBuildTest(path);
        JsonElement mismatch = result.GetProperty("mismatches").EnumerateArray().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GetProperty("pass").GetBoolean(), Is.False);
            Assert.That(result.GetProperty("built").GetInt32(), Is.EqualTo(1));
            Assert.That(mismatch.GetProperty("index").GetInt32(), Is.Zero);
            Assert.That(mismatch.GetProperty("field").GetString(), Is.EqualTo("stateRoot"));
            Assert.That(mismatch.GetProperty("expected").GetString(), Is.EqualTo(wrongRoot));
            Assert.That(mismatch.GetProperty("got").GetString(), Is.EqualTo(builtRoot).IgnoreCase);
            // The fixture's payload no longer matches its own block hash, so the client refuses to import it.
            Assert.That(result.GetProperty("rejections").EnumerateArray().Select(static r => r.GetProperty("index").GetInt32()),
                Is.EqualTo(new[] { 0 }));
        }
    }

    [Test]
    public async Task Fails_on_a_mismatch_alone()
    {
        // A field the payload decoding ignores but the comparison reads: the payload still imports, so only the
        // mismatch fails the fixture.
        JsonNode fixtures = JsonNode.Parse(File.ReadAllText(Fixture(ValidFixture)))!;
        fixtures.AsObject().Single().Value!["engineNewPayloads"]![0]!["params"]![0]!["unknownField"] = "0x01";
        string path = Path.Combine(_directory, "mismatch_alone.json");
        File.WriteAllText(path, fixtures.ToJsonString());

        JsonElement result = await RunBuildTest(path);

        JsonElement mismatch = result.GetProperty("mismatches").EnumerateArray().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GetProperty("pass").GetBoolean(), Is.False);
            Assert.That(result.GetProperty("rejections").GetArrayLength(), Is.Zero);
            Assert.That(mismatch.GetProperty("field").GetString(), Is.EqualTo("unknownField"));
            Assert.That(mismatch.GetProperty("expected").GetString(), Is.EqualTo("0x01"));
            Assert.That(mismatch.GetProperty("got").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
    }

    [TestCase("--bal-sequential")]
    [TestCase("--bal-report")]
    public async Task Takes_none_of_the_bal_flags(string flag)
    {
        (string stdout, string stderr) = await NethtestProcess.Run(["buildtest", Fixture(ValidFixture), flag, "--neverTrace"]);
        Assert.That(stderr, Does.Contain("do not apply to buildtest"), stdout);
    }

    private static string Fixture(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name);

    private static async Task<JsonElement> RunBuildTest(string fixture)
    {
        (string stdout, string stderr) = await NethtestProcess.Run(["buildtest", fixture, "--neverTrace"]);
        using JsonDocument document = JsonDocument.Parse(stdout);
        Assert.That(document.RootElement.GetArrayLength(), Is.EqualTo(1), stderr);
        return document.RootElement[0].Clone();
    }
}
