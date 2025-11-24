using PPM_Compression.Collections;
using System;
using System.Collections;
using System.Text;
using System.Windows.Controls;

namespace PPM_Compression.Compression
{
    internal class PPM
    {
        private static readonly int ORDER = 2;

        public static CompressedPPM PPMCompression(List<byte> bytes, IProgress<int> progress = null)
        {
            var res = new LinkedList<byte>();
            var context = new Dictionary<string, SortedDictionary<Option, int>>();
            var state = new CompressedPPM();
            state.BytesLen = bytes.Count;

            var esc = new Option { Value = null };

            var bytesString = Encoding.UTF8.GetString(bytes.ToArray());
            uint low = 0;
            uint high = 0xFFFFFFFF;

            for (int i = 0; i < bytesString.Length; i++)
            {
                var elem = new Option { Value = bytes[i] };
                int ctx_amount = (i < ORDER ? i : ORDER) + 1;
                string[] contexts = new string[ctx_amount];
                contexts[0] = string.Empty;
                for (int j = 1; j < ctx_amount; j++)
                {
                    contexts[j] = bytesString.Substring(i - j, j);
                }

                for (int j = ctx_amount - 1; j >= -1; j--)
                {
                    if (j == -1 || (context.ContainsKey(contexts[j]) && context[contexts[j]].ContainsKey(elem)))
                    {
                        ulong width = (ulong)high - low + 1;

                        uint total;
                        uint cumFreq;
                        uint freq;
                        if (j == -1)
                        {
                            total = 256;
                            cumFreq = bytes[i];
                            freq = 1;
                        }
                        else
                        {
                            var ctx = context[contexts[j]];
                            total = (uint)ctx.Values.Sum();
                            cumFreq = 0;
                            foreach (var kvp in ctx)
                            {
                                if (kvp.Key.Equals(elem)) break;
                                cumFreq += (uint)kvp.Value;
                            }

                            freq = (uint)ctx[elem];
                        }

                        low += (uint)(width * cumFreq / total);
                        high = low + (uint)(width * freq / total) - 1;
                        break;
                    }
                    else
                    {
                        if (context.ContainsKey(contexts[j]))
                        {
                            var currentDict = context[contexts[j]];

                            if (currentDict.ContainsKey(esc))
                            {
                                ulong width = (ulong)high - low + 1;
                                uint total = (uint)currentDict.Values.Sum();
                                uint escCount = (uint)currentDict[esc];

                                high = low + (uint)(width * escCount / total) - 1;

                                SaveCertainBits(ref low, ref high, state);
                                currentDict[esc]++;
                            }
                            else
                            {
                                currentDict.Add(esc, 1);
                            }
                        }
                        else
                        {
                            context.Add(contexts[j], new SortedDictionary<Option, int>());
                            context[contexts[j]].Add(esc, 1);
                        }
                    }
                }

                // Save older bits that are certain
                SaveCertainBits(ref low, ref high, state);

                // Add byte to context
                AddNonEscElemToContext(context, elem, contexts);

                if (i % 1000 == 0)
                    progress?.Report(i);
            }

            uint code = low + (high - low) / 2;
            for (int i = 0; i < 32; i++)
            {
                state.AddBits((byte)((code & 0x80000000) >> 31));
                code <<= 1;
            }

            return state;
        }

        public static List<byte> PPMRestore(CompressedPPM compressed, IProgress<int> progress = null)
        {
            compressed.FinalizeArr();
            var res = "";
            uint code = compressed.GetCode();
            var context = new Dictionary<string, SortedDictionary<Option, int>>();
            uint low = 0;
            uint high = 0xFFFFFFFF;

            var esc = new Option { Value = null };

            for (int i = 0; i < compressed.BytesLen; i++)
            {
                int ctx_amount = (i < ORDER ? i : ORDER) + 1;
                string[] contexts = new string[ctx_amount];
                contexts[0] = string.Empty;

                for (int j = 1; j < ctx_amount; j++)
                {
                    contexts[j] = res.Substring(i - j, j);
                }

                byte curByte = 0;

                for (int j = ctx_amount - 1; j >= -1; j--)
                {
                    ulong width = (ulong)high - low + 1;
                    uint r = code - low;

                    if (j == -1)
                    {
                        uint total = 256;

                        uint targetIndex = (uint)((ulong)r * total / width);

                        curByte = (byte)targetIndex;
                        low += (uint)(width * targetIndex / total);
                        high = low + (uint)(width / total) - 1;
                    }
                    else if (!context.ContainsKey(contexts[j]))
                    {
                        context.Add(contexts[j], new SortedDictionary<Option, int>());
                        context[contexts[j]].Add(esc, 1);
                    }
                    else
                    {
                        var ctx = context[contexts[j]];
                        uint total = (uint)ctx.Values.Sum();
                        uint targetCumFreq = (uint)((ulong)r * total / width);
                        bool done = false;

                        uint curSum = 0;
                        foreach (var kvp in context[contexts[j]])
                        {
                            ulong freq = (ulong)kvp.Value;
                            if (targetCumFreq < curSum + freq)
                            {
                                low += (uint)(width * curSum / total);
                                high = low + (uint)(width * freq / total) - 1;
                                if (kvp.Key.Value.HasValue)
                                {
                                    curByte = kvp.Key.Value.Value;
                                    done = true;
                                }
                                else
                                {
                                    LoadCertainBits(ref low, ref high, ref code, compressed);
                                    context[contexts[j]][esc]++;
                                    done = false;
                                }
                                break;
                            }
                            curSum += (uint)freq;
                        }

                        if (done)
                        {
                            break;
                        }
                    }
                }

                res += (char)curByte;

                LoadCertainBits(ref low, ref high, ref code, compressed);

                AddNonEscElemToContext(context, new Option { Value = curByte }, contexts);

                if (i % 1000 == 0)
                    progress?.Report(i);
            }


            return Encoding.UTF8.GetBytes(res).ToList();
        }

        static private void SaveCertainBits(ref uint low, ref uint high, CompressedPPM state)
        {
            while (true)
            {
                if ((low & 0x80000000) == (high & 0x80000000))
                {
                    state.AddBits((byte)((high & 0x80000000) >> 31));
                    low <<= 1;
                    high = (high << 1) | 1;
                }
                else
                {
                    break;
                }
            }
        }

        static private void LoadCertainBits(ref uint low, ref uint high, ref uint code, CompressedPPM state)
        {
            while (true)
            {
                if ((low & 0x80000000) == (high & 0x80000000))
                {
                    low <<= 1;
                    high = (high << 1) | 1;

                    code <<= 1;
                    code |= state.PopBit();
                }
                else
                {
                    break;
                }
            }
        }

        static private void AddNonEscElemToContext(Dictionary<string, SortedDictionary<Option, int>> context, Option elem, string[] contexts)
        {
            for (int j = 0; j < contexts.Length; j++)
            {
                if (!context.ContainsKey(contexts[j]))
                {
                    context.Add(contexts[j], new SortedDictionary<Option, int>());
                }

                if (context[contexts[j]].ContainsKey(elem))
                    context[contexts[j]][elem]++;
                else
                    context[contexts[j]].Add(elem, 1);
            }
        }
    }

    class Option : IEquatable<Option>, IComparable<Option>
    {
        public byte? Value { get; set; }

        public bool Equals(Option? other)
        {
            if (other is null) return false;
            return Value == other.Value;
        }

        public override int GetHashCode() => Value.HasValue ? Value.Value.GetHashCode() : 0;

        public int CompareTo(Option? other)
        {
            if (other == null) return 1;
            if (this.Value == other.Value) return 0;

            if (!this.Value.HasValue) return -1;
            if (!other.Value.HasValue) return 1;

            return this.Value.Value.CompareTo(other.Value.Value);
        }
    }
}
