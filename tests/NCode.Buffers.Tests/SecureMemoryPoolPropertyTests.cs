// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class SecureMemoryPoolPropertyTests
{
    private const string Seed = "20261115,12345";

    #region Rent Tests

    [Property(Replay = Seed)]
    public bool Rent_AfterReuse_EveryDisposeZeroesItsBufferAndPoolsItOnce(byte[] operations)
    {
        // a Gen2 trim can't empty the pool mid-test
        using var pool = new SecureMemoryPool<byte> { HighPressureThreshold = 2.0 };

        List<(IMemoryOwner<byte> Owner, byte Marker)> live = [];
        List<IMemoryOwner<byte>> disposed = [];
        HashSet<byte[]> arrays = new(ReferenceEqualityComparer.Instance);
        var rentals = 0;

        foreach (var operation in operations)
        {
            switch (operation % 3)
            {
                case 0:
                {
                    var owner = pool.Rent(16);
                    var marker = (byte)(rentals++ % 255 + 1);
                    owner.Memory.Span.Fill(marker);
                    arrays.Add(SecureMemoryPoolTests.GetArray(owner));
                    live.Add((owner, marker));
                    break;
                }

                case 1 when live.Count > 0:
                {
                    var index = operation / 3 % live.Count;
                    var (owner, _) = live[index];
                    live.RemoveAt(index);

                    var memory = owner.Memory;
                    owner.Dispose();

                    if (memory.Span.ContainsAnyExcept((byte)0))
                        return false;

                    disposed.Add(owner);
                    break;
                }

                case 2 when disposed.Count > 0:
                {
                    disposed[operation / 3 % disposed.Count].Dispose();
                    break;
                }
            }

            foreach (var (owner, marker) in live)
            {
                if (owner.Memory.Span.ContainsAnyExcept(marker))
                    return false;
            }
        }

        foreach (var (owner, _) in live)
        {
            owner.Dispose();
        }

        return pool.MemoryQueue.Count == arrays.Count
            && new HashSet<object>(pool.MemoryQueue, ReferenceEqualityComparer.Instance).Count
                == arrays.Count;
    }

    #endregion
}
