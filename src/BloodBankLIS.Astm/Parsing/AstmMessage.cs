using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Astm.Parsing;

public sealed class AstmMessage
{
    public AstmMessage(IReadOnlyList<AstmRecord> records, string raw)
    {
        Records = records;
        Raw = raw;
    }

    public IReadOnlyList<AstmRecord> Records { get; }

    public string Raw { get; }

    public InstrumentMessageKind Kind
    {
        get
        {
            if (Records.Any(r => r.Type == 'R'))
            {
                return InstrumentMessageKind.Result;
            }

            if (Records.Any(r => r.Type == 'Q'))
            {
                return InstrumentMessageKind.Query;
            }

            if (Records.Any(r => r.Type == 'O'))
            {
                return InstrumentMessageKind.Order;
            }

            return InstrumentMessageKind.Unknown;
        }
    }
}
