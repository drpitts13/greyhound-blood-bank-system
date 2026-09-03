using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Polls enabled inbound file-drop endpoints, runs each file through
/// <see cref="Hl7InboundProcessor"/>, writes an ACK, and archives the original.
/// </summary>
public sealed class Hl7FileDropInboundPoller
{
    private readonly IRepository<InterfaceEndpoint> _endpoints;
    private readonly Hl7InboundProcessor _processor;

    public Hl7FileDropInboundPoller(IRepository<InterfaceEndpoint> endpoints, Hl7InboundProcessor processor)
    {
        _endpoints = endpoints;
        _processor = processor;
    }

    public async Task<int> PollAsync(int maxBatch = 50, CancellationToken ct = default)
    {
        var endpoints = await _endpoints.ListAsync(
            e => e.IsEnabled && e.Direction == Hl7Direction.Inbound && e.Transport == InterfaceTransport.File,
            ct);

        var processed = 0;
        foreach (var endpoint in endpoints.OrderBy(e => e.Name))
        {
            if (!Hl7FileDropLayout.HasPath(endpoint.Path) || processed >= maxBatch)
            {
                continue;
            }

            var root = endpoint.Path!.Trim();
            Hl7FileDropIO.EnsureLayout(root);

            foreach (var file in Hl7FileDropIO.ListInbox(root))
            {
                if (processed >= maxBatch)
                {
                    break;
                }

                string raw;
                try
                {
                    raw = await File.ReadAllTextAsync(file, ct);
                }
                catch (IOException)
                {
                    continue;
                }

                var outcome = await _processor.ProcessAsync(raw, endpoint.Id, isReplay: false, ct);
                try
                {
                    Hl7FileDropIO.WriteAck(root, Path.GetFileName(file), outcome.AckMessage);
                    if (outcome.Accepted)
                    {
                        Hl7FileDropIO.ArchiveProcessed(root, file);
                    }
                    else
                    {
                        Hl7FileDropIO.ArchiveError(root, file);
                    }
                }
                catch (IOException)
                {
                    // Message is already logged; leave the file for the next poll.
                }

                processed++;
            }
        }

        return processed;
    }
}
