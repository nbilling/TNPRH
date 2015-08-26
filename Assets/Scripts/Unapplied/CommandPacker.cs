using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

public class Command
{
	/// <summary>
	/// Size of a command, in bytes.
	/// </summary>
	public const int PackedSize =
		sizeof(double)
        + sizeof(float)
		+ sizeof(float)
		+ sizeof(bool)
		+ sizeof(float)
		+ sizeof(float)
        + sizeof(bool)
        + sizeof(bool)
        + sizeof(bool)
        + sizeof(bool)
        + sizeof(bool);
	
    public double timestamp;
	public float h;
	public float v;
	public bool sneak;
	public float mouseX;
	public float mouseY;
    public bool mouse0;
    public bool mouse0Down;
    public bool mouse1;
    public bool equip1;
    public bool equip2;
	
	/// <summary>
	/// Construct Command from deserialized data.
	/// </summary>
	/// <param name="horizontal"></param>
	/// <param name="vertical"></param>
	/// <param name="isSneaking"></param>
	public Command(double timestamp, float horizontal, float vertical, bool isSneaking, float mouseXAxis, float mouseYAxis, bool mouse0, bool mouse0Down, bool mouse1, bool equip1, bool equip2)
	{
        this.timestamp = timestamp;
		h = horizontal;
		v = vertical;
		sneak = isSneaking;
		mouseX = mouseXAxis;
		mouseY = mouseYAxis;
        this.mouse0 = mouse0;
        this.mouse0Down = mouse0Down;
        this.mouse1 = mouse1;
        this.equip1 = equip1;
        this.equip2 = equip2;
	}
	
	/// <summary>
	/// Construct Command from serialized data.
	/// </summary>
	/// <param name="bytes"></param>
	public Command(byte[] bytes)
	{
        timestamp = BitConverter.ToDouble(bytes, 0);
        h = BitConverter.ToSingle(bytes, 0 + sizeof(double));
        v = BitConverter.ToSingle(bytes, 0 + sizeof(double) + sizeof(float));
        sneak = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float));
        mouseX = BitConverter.ToSingle(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool));
        mouseY = BitConverter.ToSingle(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float));
        mouse0 = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float) + sizeof(float));
        mouse0Down = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float) + sizeof(float) + sizeof(bool));
        mouse1 = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(bool));
        equip1 = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(bool) + sizeof(bool));
        equip2 = BitConverter.ToBoolean(bytes, 0 + sizeof(double) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(float) + sizeof(float) + sizeof(bool) + sizeof(bool) + sizeof(bool) + sizeof(bool));
	}
	
	// TODO: this could be optimized by reading from the stream directly and eliminating the copy
	public static Command Deserialize(Stream stream)
	{
		byte[] buffer = new byte[Command.PackedSize];
		
		stream.Read(buffer, 0, Command.PackedSize);
		
		return new Command(buffer);
	}
	
	public void Serialize(Stream stream)
	{
        Serialize(stream, timestamp);
		Serialize(stream, h);
		Serialize(stream, v);
		Serialize(stream, sneak);
		Serialize(stream, mouseX);
		Serialize(stream, mouseY);
        Serialize(stream, mouse0);
        Serialize(stream, mouse0Down);
        Serialize(stream, mouse1);
        Serialize(stream, equip1);
        Serialize(stream, equip2);
	}
	
	// TODO: this could be optimized by writing to the stream directly and eliminating the copy
	private static void Serialize(Stream stream, float x)
	{
		byte[] bytes = BitConverter.GetBytes(x);
		stream.Write(bytes, 0, sizeof(float));
	}
	
    // TODO: this could be optimized by writing to the stream directly and eliminating the copy
    private static void Serialize(Stream stream, double x)
    {
        byte[] bytes = BitConverter.GetBytes(x);
        stream.Write(bytes, 0, sizeof(double));
    }

	// TODO: this could be optimized by writing to the stream directly and eliminating the copy
	private static void Serialize(Stream stream, bool x)
	{
		byte[] bytes = BitConverter.GetBytes(x);
		stream.Write(bytes, 0, sizeof(bool));
	}
}

/// <summary>
/// This utility class can pack a list of commands into an array of ints and unpack an array
/// of ints into a list of commands. It is used to pack command data for Unity RPC since bytes
/// are not supported.
/// </summary>
public static class CommandPacker
{
	unsafe public static int[] Pack(IEnumerable<Command> commands)
	{
		int packedLengthInBytes = commands.Count() * Command.PackedSize;
		int packedLengthInInts = (int)Math.Ceiling((float)packedLengthInBytes / (sizeof(int)));
		
		int[] data = new int[packedLengthInInts];
		
		fixed (int* ipData = data)
		{
			byte* bpData = (byte*)ipData;
			
			Stream stream = new UnmanagedMemoryStream(bpData, packedLengthInBytes, packedLengthInBytes, FileAccess.Write);
			foreach (var command in commands)
			{
				command.Serialize(stream);
			}
		}
		
		return data;
	}
	
	unsafe public static IEnumerable<Command> Unpack(int[] data)
	{
		int packedLengthInBytes = data.Length * sizeof(int);
		int commandCount = packedLengthInBytes / Command.PackedSize;
		
		Command[] commands = new Command[commandCount];
		
		fixed (int* pData = data)
		{
			byte* bpData = (byte*)pData;
			UnmanagedMemoryStream stream = new UnmanagedMemoryStream(bpData, packedLengthInBytes, packedLengthInBytes, FileAccess.Read);
			
			for (int i = 0; i < commandCount; i++)
			{
				commands[i] = Command.Deserialize(stream);
			}
		}
		
		return commands;
	}
}
