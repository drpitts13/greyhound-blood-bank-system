using BloodBankLIS.Application.Compatibility;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-471: the Record crossmatch picker offers the patient's reserved units and the compatible units in stock,
// each once; when the compatible list cannot be read every unit stays offered and the note says why.
public class CrossmatchUnitOfferTests
{
    [Fact]
    public void Offer_IsReservedPlusCompatible_CountedOnce()
    {
        var offer = CrossmatchUnitOffer.Build([1, 2], [2, 3, 4]);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, offer.Ids!.OrderBy(i => i).ToArray());
        Assert.Equal("2 units reserved for this patient, and 2 more compatible units in stock.", offer.Note);
    }

    [Fact]
    public void Offer_Wording_ForOneAndNone()
    {
        Assert.Equal("1 unit reserved for this patient.", CrossmatchUnitOffer.Build([7], [7]).Note);
        Assert.Equal("0 units reserved for this patient, and 1 more compatible unit in stock.", CrossmatchUnitOffer.Build([], [9]).Note);
        Assert.Empty(CrossmatchUnitOffer.Build([], []).Ids!);
    }

    [Fact]
    public void UnreadableCompatibleList_OffersEveryUnit_AndSaysWhy()
    {
        var offer = CrossmatchUnitOffer.Build([1], null, "Patient has no current ABO/Rh on file; compatible units cannot be listed.");
        Assert.Null(offer.Ids);
        Assert.Equal("Showing every unit: Patient has no current ABO/Rh on file; compatible units cannot be listed. Recording still checks the unit.", offer.Note);
    }
}
