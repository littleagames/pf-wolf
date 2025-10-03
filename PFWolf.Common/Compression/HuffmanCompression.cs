using System.Text;

namespace PFWolf.Common.Compression;

public class HuffmanTree
{
    public HuffmanNode Head { get; set; }

    public HuffmanTree(byte[] dictionaryData)
    {
        // TODO: There could be a possibility this dictionary is bigger than 1024 file??
        // Therefore I could just collect all of the nodes, and then find the head node, and connect all of them
        ushort headPosition = 254;
        var frequency = 0;
        Head = HuffmanNode.CreateNode(headPosition, frequency, dictionaryData);
    }

    public StringBuilder Display()
    {
        var sb = new StringBuilder();
        Head?.PrintPretty(sb, string.Empty, last: false);
        return sb;
    }
    public StringBuilder DisplayLeaves()
    {
        var sb = new StringBuilder();
        Head?.PrintLeaves(sb);
        return sb;
    }
    public string Traverse(byte value)
    {
        var shortVal = 0;
        var bitString = "";
        var result = HuffmanNode.CheckNodeValue(Head, value, ref shortVal, ref bitString);
        return bitString;
        //return (ushort)shortVal;
    }
}

public class HuffmanNode
{
    public HuffmanNode? Left { get; private set; } = null;
    public HuffmanNode? Right { get; private set; } = null;
    public int Frequency { get; private set; }
    public bool IsLeaf { get; private set; }

    public ushort Value { get; private set; }

    public void PrintLeaves(StringBuilder builder)
    {
        if (IsLeaf)
        {
            builder.AppendLine($"Value: {Value}, Frequency: {Frequency}");
        }
        else
        {
            Left?.PrintLeaves(builder);
            Right?.PrintLeaves(builder);
        }
    }

    public void PrintPretty(StringBuilder builder, string indent, bool last)
    {
        builder.Append(indent);
        if (last)
        {
            builder.Append("\\-");
            indent += "  ";
        }
        else
        {
            builder.Append("|-");
            indent += "| ";
        }
        if (IsLeaf)
        {
            builder.AppendLine($"Value: {Value}, Leaf: Yes");
        } else
        {
            builder.AppendLine($"Value: {Value}");
        }

        Left?.PrintPretty(builder, indent, last);
        Right?.PrintPretty(builder, indent, last);
    }
    private HuffmanNode()
    {   
    }

    public static HuffmanNode? CheckNodeValue(HuffmanNode node, byte value, ref int shortVal, ref string bitVal)
    {
        if (node == null) return null;

        if (node.IsLeaf && node.Value == value)
        {
           // bitVal = 15;
            return node;
        }

        var left = CheckNodeValue(node.Left, value, ref shortVal, ref bitVal);
        if (left != null)
        {
            //var mask = 1 << --bitVal;
            //shortVal &= ~mask;
            bitVal += "0";
            return left;
        }

        var right = CheckNodeValue(node.Right, value, ref shortVal, ref bitVal);
        if (right != null)
        {
            //var mask = 1 << --bitVal;
            //shortVal |= mask;
            bitVal += "1";
            return right;
        }

        return null;
    }

    public static HuffmanNode CreateNode(ushort value, int frequency, HuffmanNode left, HuffmanNode right)
    {
        return new HuffmanNode()
        {
            Value = 255,//value,
            Frequency = frequency,
            Left = left,
            Right = right
        };
    }
    public static HuffmanNode CreateNode(ushort value, int frequency, byte[] dictionaryData)
    {
        var node = new HuffmanNode()
        {
            Value = value,
            Frequency = frequency,
        };
        var childNodeFreqency = frequency + 1;

        var nodeData = dictionaryData.Skip(value * 4).Take(4).ToArray();
        var leftValue = nodeData[0]; // left
        var leftIsLeaf = nodeData[1] == 0; // 0 == left.isLeaf
        var left = leftIsLeaf ? CreateLeaf(leftValue, childNodeFreqency) : CreateNode(leftValue, childNodeFreqency, dictionaryData);
        node.Left = left;

        var rightValue = nodeData[2]; // right
        var rightIsLeaf = nodeData[3] == 0; // 0 == right.isLeaf
        var right = rightIsLeaf ? CreateLeaf(rightValue, childNodeFreqency) : CreateNode(rightValue, childNodeFreqency, dictionaryData);
        node.Right = right;

        return node;
    }

    public static HuffmanNode CreateLeaf(ushort value, int frequency)
    {
        return new HuffmanNode()
        {
            Value = value,
            Frequency = frequency,
            IsLeaf = true
        };
    }
}

public class HuffmanCompression //: ICompression<byte>
{
    private byte[] DictionaryFile { get; }
    private HuffmanTree Tree { get; }

    public string DisplayTree()
    {
        return Tree?.Display().ToString() ?? "N/A";
    }

    public string DisplayLeaves()
    {
        return Tree?.DisplayLeaves().ToString() ?? "N/A";
    }

    public HuffmanCompression(byte[] dictionaryFile)
    {
        DictionaryFile = dictionaryFile;
        Tree = new HuffmanTree(DictionaryFile);
    }

    public byte[] Expand(byte[] source)
    {
        List<byte> dest = new List<byte>();
        int sourceIndex = 0;
        byte val = source[sourceIndex++];
        byte mask = 1;
        HuffmanNode nodeval;
        
        HuffmanNode node = Tree.Head;

        while (true)
        {
            // check if the val matches the mask bit (2^n)
            if ((val & mask) == 0)
                nodeval = node.Left;
            else
                nodeval = node.Right;
            if (mask == 0x80) // if mask is 128, 2^7
            {
                if (sourceIndex >= source.Length)
                    break;
                val = source[sourceIndex++]; // get next source void
                mask = 1;           // start mask back to 1
            }
            else mask <<= 1; // bit shift mask left by 1

            if (nodeval.IsLeaf)
            {
                dest.Add((byte)nodeval.Value);
                node = Tree.Head; // start huffptr back at head
            }
            else
            {
                // set ptr to table[nodeval- 256]
                node = nodeval;
            }
        }

        return dest.ToArray();
    }

    public byte[] Compress(byte[] source)
    {
        var dest = new List<byte>();
        StringBuilder bitString = new StringBuilder();
        //var i = 3;
        for (int i = 0; i < source.Length; i++)
        {
            var shortVal = Tree.Traverse(source[i]);
            bitString.Append(shortVal);
            //if (shortVal > 0)
             {
                //dest.AddRange(BitConverter.GetBytes(shortVal).Where(x => x > 0));
            }
        }
        return dest.ToArray();
    }
}
