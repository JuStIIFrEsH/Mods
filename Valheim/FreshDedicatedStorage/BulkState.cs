using System;
using System.Globalization;
using System.Numerics;

namespace Mike.Valheim.SmallStorageChest
{
    // One versioned ZDO string keeps type, item metadata and quantity together.
    // BigInteger avoids both normal stack limits and integer overflow.
    internal sealed class BulkState
    {
        public BigInteger Count { get; }
        public string Template { get; }
        public BulkState(BigInteger count, string template)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > 0 && string.IsNullOrEmpty(template)) throw new ArgumentException("Missing item template.");
            Count = count;
            Template = template ?? "";
        }
        public static BulkState Empty => new BulkState(BigInteger.Zero, "");
        public string Encode() => "1|" + Count.ToString(CultureInfo.InvariantCulture) + "|" + Template;
        public static BulkState Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return Empty;
            string[] parts = text.Split('|');
            if (parts.Length != 3 || parts[0] != "1" ||
                !BigInteger.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                throw new FormatException("Invalid bulk box data; preserved without modification.");
            if (parts[2].Length > 0) Convert.FromBase64String(parts[2]);
            return new BulkState(count, parts[2]);
        }
        public BulkState Deposit(string template, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!string.IsNullOrEmpty(Template) && Template != template)
                throw new InvalidOperationException("This box is assigned to a different item or variant.");
            return new BulkState(Count + amount, template);
        }
        public BulkState Withdraw(BigInteger amount)
        {
            if (amount <= 0 || amount > Count) throw new ArgumentOutOfRangeException(nameof(amount));
            return new BulkState(Count - amount, Template);
        }
        public BulkState AssignFromSlot(string template, int amount)
        {
            // An empty box may be reassigned, but never replace an occupied box's contents.
            return (Count.IsZero ? Empty : this).Deposit(template, amount);
        }
    }
}
