using BloodBankLIS.Astm.Parsing;

namespace BloodBankLIS.Astm.Simulator;

public sealed record EchoSimulatorRequest(
    string Assay,
    string SpecimenId,
    string? PatientId,
    string? LastName,
    string? FirstName,
    string? DateOfBirth,
    string? Sex,
    int? Seed = null);

public sealed record EchoSimulatorResult(string Assay, string RawMessage, IReadOnlyList<string> Records);

/// <summary>Builds randomized ImmuLINK Echo result messages from the LIS specification examples.</summary>
public static class EchoResultSimulator
{
    private static readonly string[] Grades = ["0", "1+", "2+", "3+", "4+"];
    private static readonly string[] GroupInterps =
        ["A Pos", "A Neg", "B Pos", "B Neg", "AB Pos", "AB Neg", "O Pos", "O Neg"];
    private static readonly string[] ScreenInterps = ["Positive", "Negative"];
    private static readonly string[] XmInterps = ["IgG Comp (Check ABO Comp)", "Incompatible"];
    private static readonly string[] DatInterps = ["Positive", "Negative"];
    private static readonly string[] WeakDInterps = ["Positive (Perform DAT Assay)", "Negative"];

    public static IReadOnlyList<string> Assays { get; } =
        ["Group", "Screen", "Crossmatch", "DAT", "Weak D", "Donor", "GroupScreen"];

    public static EchoSimulatorResult Generate(EchoSimulatorRequest request)
    {
        var rng = request.Seed is int seed ? new Random(seed) : Random.Shared;
        var now = DateTime.UtcNow;
        var records = new List<string>
        {
            AstmBuilder.Header("ImmuLINK", "LIS", now),
            AstmBuilder.Patient(
                1,
                request.PatientId ?? "SIM001",
                request.LastName ?? "Patient",
                request.FirstName ?? "Test",
                request.DateOfBirth ?? "19590422",
                request.Sex ?? "M")
        };

        var assay = string.IsNullOrWhiteSpace(request.Assay) ? "GroupScreen" : request.Assay.Trim();
        if (string.Equals(assay, "GroupScreen", StringComparison.OrdinalIgnoreCase))
        {
            AddGroup(records, request.SpecimenId, now, rng);
            AddScreen(records, request.SpecimenId, now, rng, sequence: 2);
        }
        else if (string.Equals(assay, "Group", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(assay, "Donor", StringComparison.OrdinalIgnoreCase))
        {
            AddGroup(records, request.SpecimenId, now, rng, assay);
        }
        else if (string.Equals(assay, "Screen", StringComparison.OrdinalIgnoreCase))
        {
            AddScreen(records, request.SpecimenId, now, rng);
        }
        else if (string.Equals(assay, "Crossmatch", StringComparison.OrdinalIgnoreCase))
        {
            AddCrossmatch(records, request.SpecimenId, now, rng);
        }
        else if (string.Equals(assay, "DAT", StringComparison.OrdinalIgnoreCase))
        {
            AddDat(records, request.SpecimenId, now, rng);
        }
        else if (string.Equals(assay, "Weak D", StringComparison.OrdinalIgnoreCase))
        {
            AddWeakD(records, request.SpecimenId, now, rng);
        }
        else
        {
            AddGroup(records, request.SpecimenId, now, rng);
        }

        records.Add(AstmBuilder.Terminator());
        return new EchoSimulatorResult(assay, AstmBuilder.Join(records.ToArray()), records);
    }

    private static void AddGroup(List<string> records, string specimenId, DateTime now, Random rng, string assay = "Group")
    {
        records.Add(AstmBuilder.Order(1, specimenId, assay, assay.Equals("Donor", StringComparison.OrdinalIgnoreCase) ? "Blood^Product" : "Blood^Patient"));
        var cells = new (string Code, string Grade)[]
        {
            ("Mono Ctrl", Grade(rng, preferNegative: true)),
            ("Anti-A", Grade(rng)),
            ("Anti-B", Grade(rng)),
            ("Anti-D1", Grade(rng)),
            ("Anti-D2", Grade(rng)),
            ("A1 Cells", Grade(rng)),
            ("B Cells", Grade(rng))
        };

        var seq = 1;
        foreach (var (code, grade) in cells)
        {
            records.Add(Reaction(seq++, code, grade, now));
        }

        var interp = assay.Equals("Donor", StringComparison.OrdinalIgnoreCase)
            ? Pick(rng, GroupInterps)
            : Pick(rng, GroupInterps);
        records.Add(AstmBuilder.Result(seq, assay, interp, "F", Stamp(now), "M0002"));
    }

    private static void AddScreen(List<string> records, string specimenId, DateTime now, Random rng, int sequence = 1)
    {
        records.Add(AstmBuilder.Order(sequence, specimenId, "Screen"));
        var seq = 1;
        foreach (var cell in new[] { "Screen 1", "Screen 2", "Screen 3", "Pos Ctrl" })
        {
            records.Add(Reaction(seq++, cell, cell == "Pos Ctrl" ? "4+" : Grade(rng), now));
        }

        records.Add(AstmBuilder.Result(seq, "Screen", Pick(rng, ScreenInterps), "F", Stamp(now), "M0002"));
    }

    private static void AddCrossmatch(List<string> records, string specimenId, DateTime now, Random rng)
    {
        records.Add(AstmBuilder.Order(1, specimenId, "Crossmatch"));
        records.Add(AstmBuilder.Result(1, "Donor", "R02460", "F", Stamp(now), "M0002"));
        records.Add(Reaction(2, "IgG XM", Grade(rng), now));
        records.Add(Reaction(3, "Ind Ctrl", "4+", now));
        records.Add(AstmBuilder.Result(4, "Crossmatch", Pick(rng, XmInterps), "F", Stamp(now), "M0002"));
    }

    private static void AddDat(List<string> records, string specimenId, DateTime now, Random rng)
    {
        records.Add(AstmBuilder.Order(1, specimenId, "DAT"));
        records.Add(Reaction(1, "DAT Rxn", Grade(rng), now));
        records.Add(Reaction(2, "Ind Ctrl", "4+", now));
        records.Add(AstmBuilder.Result(3, "DAT", Pick(rng, DatInterps), "F", Stamp(now), "M0002"));
    }

    private static void AddWeakD(List<string> records, string specimenId, DateTime now, Random rng)
    {
        records.Add(AstmBuilder.Order(1, specimenId, "Weak D"));
        records.Add(Reaction(1, "Anti-D", Grade(rng), now));
        records.Add(Reaction(2, "Ind Ctrl", Grade(rng, preferNegative: true), now));
        records.Add(AstmBuilder.Result(3, "Weak D", Pick(rng, WeakDInterps), "F", Stamp(now), "M0002"));
    }

    private static string Reaction(int seq, string analyte, string grade, DateTime now)
    {
        var measured = grade switch
        {
            "0" => 0,
            "1+" => 15,
            "2+" => 35,
            "3+" => 55,
            "4+" => 90,
            _ => 0
        };
        return AstmBuilder.Result(seq, analyte, $"{grade}^{grade}^{measured}", "F", Stamp(now), "M0002");
    }

    private static string Grade(Random rng, bool preferNegative = false) =>
        preferNegative && rng.NextDouble() < 0.8 ? "0" : Pick(rng, Grades);

    private static string Pick(Random rng, IReadOnlyList<string> values) => values[rng.Next(values.Count)];

    private static string Stamp(DateTime utc) => utc.ToString("yyyyMMddHHmmss");
}
