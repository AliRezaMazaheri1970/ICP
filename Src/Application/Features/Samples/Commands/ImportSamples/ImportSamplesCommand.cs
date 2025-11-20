// مسیر فایل: Application/Features/Samples/Commands/ImportSamples/ImportSamplesCommand.cs

using MediatR;
using Shared.Wrapper;
using System.IO;

namespace Application.Features.Samples.Commands.ImportSamples;

public record ImportSamplesCommand(Guid ProjectId, Stream FileStream, string FileName) : IRequest<Result<int>>;