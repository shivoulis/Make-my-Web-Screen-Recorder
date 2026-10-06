// Makes newer ONNX models loadable by the ML engine built into Windows (Windows ML):
//  - stores weights kept in an external ".data" file inside the model (Windows ML loads models
//    from memory, so it cannot resolve external files)
//  - lowers the IR version (10 -> 9) and opset (21 -> 20) stamps; the operators used by the
//    segmentation model are identical in those versions apart from extra data types
// Works directly on the protobuf encoding, so no ONNX libraries are needed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MakeMyWebRecorder {

static class OnnxFix {
    public static byte[] Convert(byte[] model, byte[] externalData) {
        var o = new MemoryStream();
        Walk(model, 0, model.Length, delegate(int field, int wire, int start, int end, int valueStart) {
            if (field == 1 && wire == 0) {                              // ir_version
                long v = ReadVarint(model, ref valueStart);
                WriteTag(o, 1, 0); WriteVarint(o, Math.Min(v, 9));
            } else if (field == 7 && wire == 2) {                       // graph
                WriteBytes(o, 7, Graph(model, valueStart, end, externalData));
            } else if (field == 8 && wire == 2) {                       // opset_import
                WriteBytes(o, 8, Opset(model, valueStart, end));
            } else {
                o.Write(model, start, end - start);
            }
        });
        return o.ToArray();
    }

    static byte[] Graph(byte[] b, int from, int to, byte[] data) {
        var o = new MemoryStream();
        Walk(b, from, to, delegate(int field, int wire, int start, int end, int valueStart) {
            if (field == 5 && wire == 2) WriteBytes(o, 5, Tensor(b, valueStart, end, data));   // initializer
            else o.Write(b, start, end - start);
        });
        return o.ToArray();
    }

    static byte[] Tensor(byte[] b, int from, int to, byte[] data) {
        var o = new MemoryStream();
        string location = null;
        long offset = 0, length = -1;
        Walk(b, from, to, delegate(int field, int wire, int start, int end, int valueStart) {
            if (field == 13 && wire == 2) {                              // external_data {key, value}
                string key = null, value = null;
                Walk(b, valueStart, end, delegate(int f2, int w2, int s2, int e2, int v2) {
                    string text = Encoding.UTF8.GetString(b, v2, e2 - v2);
                    if (f2 == 1) key = text; else if (f2 == 2) value = text;
                });
                if (key == "location") location = value;
                else if (key == "offset") offset = long.Parse(value);
                else if (key == "length") length = long.Parse(value);
            } else if (field == 14 && wire == 0) {
                // data_location: dropped, the data becomes inline
            } else {
                o.Write(b, start, end - start);
            }
        });
        if (location != null) {
            if (data == null) throw new InvalidDataException("The model needs its external data file.");
            if (length < 0) length = data.Length - offset;
            var raw = new byte[length];
            Array.Copy(data, offset, raw, 0, length);
            WriteBytes(o, 9, raw);                                       // raw_data
        }
        return o.ToArray();
    }

    static byte[] Opset(byte[] b, int from, int to) {
        var o = new MemoryStream();
        string domain = "";
        Walk(b, from, to, delegate(int field, int wire, int start, int end, int valueStart) {
            if (field == 1 && wire == 2) domain = Encoding.UTF8.GetString(b, valueStart, end - valueStart);
        });
        Walk(b, from, to, delegate(int field, int wire, int start, int end, int valueStart) {
            if (field == 2 && wire == 0 && domain == "") {
                long v = ReadVarint(b, ref valueStart);
                WriteTag(o, 2, 0); WriteVarint(o, Math.Min(v, 20));
            } else {
                o.Write(b, start, end - start);
            }
        });
        return o.ToArray();
    }

    // ---- protobuf wire format ----

    delegate void FieldHandler(int field, int wire, int start, int end, int valueStart);

    // Calls back for each field in b[from..to): start/end cover the whole field, valueStart the payload.
    static void Walk(byte[] b, int from, int to, FieldHandler handler) {
        int p = from;
        while (p < to) {
            int start = p;
            long key = ReadVarint(b, ref p);
            int field = (int)(key >> 3), wire = (int)(key & 7);
            int valueStart = p;
            switch (wire) {
                case 0: ReadVarint(b, ref p); break;
                case 1: p += 8; break;
                case 2: { long len = ReadVarint(b, ref p); valueStart = p; p += (int)len; break; }
                case 5: p += 4; break;
                default: throw new InvalidDataException("Unsupported protobuf wire type " + wire);
            }
            handler(field, wire, start, p, valueStart);
        }
    }

    static long ReadVarint(byte[] b, ref int p) {
        long v = 0;
        int shift = 0;
        while (true) {
            byte x = b[p++];
            v |= (long)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return v;
            shift += 7;
        }
    }

    static void WriteVarint(Stream s, long v) {
        ulong u = (ulong)v;
        while (u >= 0x80) { s.WriteByte((byte)(u | 0x80)); u >>= 7; }
        s.WriteByte((byte)u);
    }

    static void WriteTag(Stream s, int field, int wire) { WriteVarint(s, (long)(((uint)field << 3) | (uint)wire)); }

    static void WriteBytes(Stream s, int field, byte[] payload) {
        WriteTag(s, field, 2);
        WriteVarint(s, payload.Length);
        s.Write(payload, 0, payload.Length);
    }
}

}
