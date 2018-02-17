﻿using System;
using System.Security.Cryptography;

namespace MiffyLiye.Snowflake
{
    public class Snowflake
    {
        public int MachineIdOffset { get; } = 42;
        public int MachineIdLength { get; } = 10;
        private long IdWithOnlyMachineId { get; }

        private long TimestampMask { get; }
        private long MachineIdMask { get; }
        private long RandomNumberMask { get; }

        private DateTime TimestampOffset { get; }
        private IClock Clock { get; }

        private object SequenceLocker { get; } = new object();
        private long SequenceEncryptionKey { get; }
        private long LastSequence { get; set; }

        public Snowflake(int machineId = 0, DateTime? timestampOffset = null, IClock clock = null)
        {
            LastSequence = 0;
            MachineIdMask =
                Convert.ToInt64(
                    new string('0', MachineIdOffset) +
                    new string('1', MachineIdLength) +
                    new string('0', 64 - MachineIdOffset - MachineIdLength),
                    2);
            var idWithOnlyUnmaskedMachineId = ((long) machineId) << (64 - MachineIdOffset - MachineIdLength);
            IdWithOnlyMachineId = idWithOnlyUnmaskedMachineId & MachineIdMask;
            if (IdWithOnlyMachineId != idWithOnlyUnmaskedMachineId)
            {
                throw new ArgumentException($"Machine ID should not be longer than {MachineIdLength} bits.");
            }

            TimestampMask = Convert.ToInt64(
                new string('0', 1) +
                new string('1', MachineIdOffset - 1) +
                new string('0', MachineIdLength) +
                new string('0', 64 - MachineIdOffset - MachineIdLength),
                2);
            RandomNumberMask =
                Convert.ToInt64(
                    new string('0', MachineIdOffset) +
                    new string('0', MachineIdLength) +
                    new string('1', 64 - MachineIdOffset - MachineIdLength),
                    2);

            TimestampOffset = timestampOffset ?? DateTime.Parse("2014-01-10 08:00:00Z");
            Clock = clock ?? new SystemClock();
            var random = new byte[2];
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(random);
            }
            SequenceEncryptionKey = BitConverter.ToInt16(random, 0) & RandomNumberMask;
        }

        public long Next()
        {
            long sequence;
            lock (SequenceLocker)
            {
                unchecked
                {
                    sequence = ++LastSequence;
                }
            }
            var idWithOnlyTimestamp = ((Clock.UtcNow.Ticks - TimestampOffset.Ticks) << 8) & TimestampMask;
            var idWithOnlyRandomNumber = (sequence ^ SequenceEncryptionKey) & RandomNumberMask;
            return idWithOnlyTimestamp | IdWithOnlyMachineId | idWithOnlyRandomNumber;
        }
    }
}
