using System.Collections.Generic;
using GpuParticle.Runtime;

namespace GpuParticle.Editor
{
    internal sealed class GpuParticleBakeReport
    {
        private readonly List<string> messages = new List<string>();

        public IReadOnlyList<string> Messages => messages;
        public GpuParticleFailure Failure { get; private set; } = GpuParticleFailure.None;
        public bool HasFailure => Failure.IsFailure;

        public void Info(string message)
        {
            messages.Add(message);
        }

        public void Fail(GpuParticleFailureCode code, string message, string context = "")
        {
            Failure = new GpuParticleFailure(code, message, context);
            messages.Add(Failure.ToString());
        }
    }
}
