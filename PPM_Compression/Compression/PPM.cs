using PPM_Compression.Collections;

namespace PPM_Compression.Compression
{
    internal class PPM
    {
        private static readonly int ORDER = 2;
        private static readonly int ESC_IDX = 256;

        public static CompressedPPM PPMCompression(List<byte> bytes, IProgress<int> progress)
        {
            var context = new Dictionary<ContextKey, FrequencyModel>();
            var state = new CompressedPPM { BytesLen = bytes.Count };

            uint low = 0;
            uint high = 0xFFFFFFFF;

            for (int i = 0; i < bytes.Count; i++)
            {
                var elem = bytes[i];
                int ctx_amount = i < ORDER ? i : ORDER;

                for (int j = ctx_amount; j >= -1; j--)
                {
                    if (j == -1)
                    {
                        ulong width = (ulong)high - low + 1;

                        uint total = 256;
                        uint cumFreq = bytes[i];

                        high = low + (uint)(width * (cumFreq + 1) / total) - 1;
                        low += (uint)(width * cumFreq / total);

                        break;
                    }

                    var key = new ContextKey(bytes, i, j);

                    if (context.TryGetValue(key, out var model))
                    {
                        ulong width = (ulong)high - low + 1;

                        uint total = model.TotalCount;

                        if (model.Counts[elem] > 0)
                        {
                            uint cumFreq = 0;
                            for (int k = 0; k < elem; k++) cumFreq += model.Counts[k];

                            uint freq = model.Counts[elem];

                            high = low + (uint)(width * (cumFreq + freq) / total) - 1;
                            low += (uint)(width * cumFreq / total);

                            break;
                        }
                        else
                        {
                            uint escCount = model.Counts[ESC_IDX];

                            high = low + (uint)(width * model.TotalCount / total) - 1;
                            low += (uint)(width * (model.TotalCount - model.Counts[ESC_IDX]) / total);

                            SaveCertainBits(ref low, ref high, state);
                            model.Increment(ESC_IDX);
                        }
                    }
                    else
                    {
                        var newModel = new FrequencyModel();
                        context.Add(key, newModel);
                    }
                }

                SaveCertainBits(ref low, ref high, state);
                AddNonEscElemToContext(context, bytes, i, elem);

                if (i % 1000 == 0)
                    progress?.Report(i);
            }

            state.FollowBits++;

            byte final_bit = (byte)((low & 0x40000000) >> 30);
            state.AddBits(final_bit);

            while (state.FollowBits > 0)
            {
                state.AddBits((byte)(final_bit ^ 1));
                state.FollowBits--;
            }

            state.FinalizeArr();

            return state;
        }

        public static List<byte> PPMRestore(CompressedPPM compressed, IProgress<int> progress)
        {
            var res = new List<byte>(compressed.BytesLen);
            uint code = compressed.GetCode();
            var context = new Dictionary<ContextKey, FrequencyModel>();
            uint low = 0;
            uint high = 0xFFFFFFFF;

            for (int i = 0; i < compressed.BytesLen; i++)
            {
                int ctx_amount = i < ORDER ? i : ORDER;

                byte curByte = 0;

                for (int j = ctx_amount; j >= -1; j--)
                {
                    ulong width = (ulong)high - low + 1;
                    uint r = code - low;

                    if (j == -1)
                    {
                        uint total = 256;

                        for (uint k = 0; k < 256; k++)
                        {
                            ulong rangeUpper = (uint)(width * (k + 1) / total);

                            if (r < rangeUpper)
                            {
                                curByte = (byte)k;

                                high = low + (uint)rangeUpper - 1;
                                low += (uint)(width * k / total);
                                break;
                            }
                        }

                        break;
                    }

                    var key = new ContextKey(res, j);

                    if (context.TryGetValue(key, out var model))
                    {
                        uint total = model.TotalCount;
                        bool done = false;
                        uint curSum = 0;
                        uint k = 0;

                        foreach (var f in model.Counts)
                        {
                            ulong freq = f;

                            if (freq > 0)
                            {
                                ulong rangeUpper = width * (curSum + freq) / total;

                                if (r < rangeUpper)
                                {
                                    high = low + (uint)rangeUpper - 1;
                                    low += (uint)(width * curSum / total);

                                    if (k == ESC_IDX)
                                    {
                                        LoadCertainBits(ref low, ref high, ref code, compressed);
                                        model.Increment(ESC_IDX);
                                        done = false;
                                    }
                                    else
                                    {
                                        curByte = (byte)k;
                                        done = true;
                                    }
                                    break;
                                }
                            }

                            curSum += (uint)freq;
                            k++;
                        }

                        if (done)
                        {
                            break;
                        }
                        
                    }
                    else
                    {
                        var newModel = new FrequencyModel();
                        context.Add(key, newModel);
                    }
                }

                res.Add(curByte);

                LoadCertainBits(ref low, ref high, ref code, compressed);

                AddNonEscElemToContext(context, res, res.Count - 1, curByte);

                if (i % 1000 == 0)
                    progress?.Report(i);
            }


            return res;
        }

        static private void SaveCertainBits(ref uint low, ref uint high, CompressedPPM state)
        {
            while (true)
            {
                if ((low & 0x80000000) == (high & 0x80000000))
                {
                    byte bit = (byte)((high & 0x80000000) >> 31);
                    state.AddBits(bit);

                    while (state.FollowBits > 0)
                    {
                        state.AddBits((byte)(bit ^ 1));
                        state.FollowBits--;
                    }

                    low <<= 1;
                    high = (high << 1) | 1;
                }
                else if ((low & 0x40000000) != 0 && (high & 0x40000000) == 0)
                {
                    state.FollowBits++;
                    low = (low & 0x3FFFFFFF) << 1;
                    high = ((high & 0x3FFFFFFF) << 1) | 0x80000001;
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
                else if ((low & 0x40000000) != 0 && (high & 0x40000000) == 0)
                {
                    low = (low & 0x3FFFFFFF) << 1;
                    high = ((high & 0x3FFFFFFF) << 1) | 0x80000001;

                    uint currentCodeTop = code & 0x80000000;
                    uint currentCodeRest = (code & 0x3FFFFFFF) << 1;

                    code = currentCodeTop | currentCodeRest | state.PopBit();
                }
                else
                {
                    break;
                }
            }
        }

        private static void AddNonEscElemToContext(Dictionary<ContextKey, FrequencyModel> contextMap, List<byte> bytes, int index, int symbol)
        {
            int maxOrder = (index < ORDER ? index : ORDER);
            for (int j = 0; j <= maxOrder; j++)
            {
                var key = new ContextKey(bytes, index, j);
                if (!contextMap.TryGetValue(key, out var model))
                {
                    model = new FrequencyModel();
                    contextMap.Add(key, model);
                }
                model.Increment(symbol);
            }
        }
    }

    class FrequencyModel
    {
        public uint[] Counts = new uint[257];
        public uint TotalCount = 0;

        public FrequencyModel()
        {
            Counts[256] = 1;
            TotalCount = 1;
        }

        public void Increment(int symbol)
        {
            Counts[symbol]++;
            TotalCount++;
        }
    }

    struct ContextKey : IEquatable<ContextKey>
    {
        private readonly int _hash;
        private readonly List<byte> _data;
        private readonly int _index;
        private readonly int _length;

        public ContextKey(List<byte> data, int index, int length)
        {
            _data = data;
            _index = index;
            _length = length;

            if (length == 0)
            {
                _hash = -1;
                return;
            }

            int h = 0;
            for (int i = 0; i < length; i++)
            {
                h = (h << 8) | data[index - length + i];
            }
            _hash = h;
        }

        public ContextKey(List<byte> data, int length) : this(data, data.Count, length) { }

        public override int GetHashCode() => _hash;

        public bool Equals(ContextKey other)
        {
            if (_hash != other._hash) return false;

            if (_length == 0 && other._length == 0) return true;

            if (_length != other._length) return false;

            for (int i = 0; i < _length; i++)
            {
                byte b1 = _data[_index - _length + i];
                byte b2 = other._data[other._index - other._length + i];
                if (b1 != b2) return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ContextKey other && Equals(other);
    }
}
