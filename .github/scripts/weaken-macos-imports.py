#!/usr/bin/env python3
"""Marks every dyld-bound import of a thin 64-bit Mach-O library as a weak import.

The prebuilt OpenCV native library imports symbols that only exist on recent macOS versions
(for example VideoToolbox's _kVTPixelRotationPropertyKey_*). dyld refuses to load the library
on older systems because of a single missing symbol. A weak import is bound to NULL instead,
so the library loads and only the unused camera/video code paths are affected.

The code signature becomes invalid after patching, so the caller must re-sign the file.
"""
import struct
import sys

MH_MAGIC_64 = 0xFEEDFACF
LC_DYLD_INFO = 0x22
LC_DYLD_INFO_ONLY = 0x80000022
LC_DYLD_CHAINED_FIXUPS = 0x80000034

BIND_OPCODE_MASK = 0xF0
BIND_IMMEDIATE_MASK = 0x0F
BIND_SYMBOL_FLAGS_WEAK_IMPORT = 0x1


def read_uleb(data, pos):
	result = 0
	shift = 0
	while True:
		byte = data[pos]
		pos += 1
		result |= (byte & 0x7F) << shift
		shift += 7
		if not byte & 0x80:
			return result, pos


def read_sleb(data, pos):
	result = 0
	shift = 0
	while True:
		byte = data[pos]
		pos += 1
		result |= (byte & 0x7F) << shift
		shift += 7
		if not byte & 0x80:
			if byte & 0x40:
				result -= 1 << shift
			return result, pos


def walk_binds(data, start, size, patch):
	"""Walks bind opcodes; returns (total symbols, symbols that were already weak, patched symbols)."""
	pos = start
	end = start + size
	total = already_weak = patched = 0
	while pos < end:
		byte = data[pos]
		opcode = byte & BIND_OPCODE_MASK
		if opcode == 0x40:  # BIND_OPCODE_SET_SYMBOL_TRAILING_FLAGS_IMM
			flags_pos = pos
			pos += 1
			while data[pos] != 0:
				pos += 1
			pos += 1
			total += 1
			if byte & BIND_SYMBOL_FLAGS_WEAK_IMPORT:
				already_weak += 1
			elif patch:
				data[flags_pos] = byte | BIND_SYMBOL_FLAGS_WEAK_IMPORT
				patched += 1
			continue
		pos += 1
		if opcode in (0x20, 0x70, 0x80, 0xA0):  # ordinal/segment+offset/add address/bind+add (ULEB)
			_, pos = read_uleb(data, pos)
		elif opcode == 0x60:  # SET_ADDEND_SLEB
			_, pos = read_sleb(data, pos)
		elif opcode == 0xC0:  # DO_BIND_ULEB_TIMES_SKIPPING_ULEB
			_, pos = read_uleb(data, pos)
			_, pos = read_uleb(data, pos)
		elif opcode == 0xD0:  # THREADED
			if byte & BIND_IMMEDIATE_MASK == 0:
				_, pos = read_uleb(data, pos)
		elif opcode not in (0x00, 0x10, 0x30, 0x50, 0x90, 0xB0):
			raise ValueError("Unknown bind opcode 0x%02X at offset %d" % (byte, pos - 1))
	return total, already_weak, patched


def process(path, patch):
	with open(path, "rb") as f:
		data = bytearray(f.read())

	magic = struct.unpack_from("<I", data, 0)[0]
	if magic != MH_MAGIC_64:
		raise ValueError("%s is not a thin 64-bit Mach-O file (magic 0x%08X)" % (path, magic))

	ncmds = struct.unpack_from("<I", data, 16)[0]
	offset = 32
	dyld_info = None
	for _ in range(ncmds):
		cmd, size = struct.unpack_from("<II", data, offset)
		if cmd == LC_DYLD_CHAINED_FIXUPS:
			raise ValueError("%s uses chained fixups, which this script does not support" % path)
		if cmd in (LC_DYLD_INFO, LC_DYLD_INFO_ONLY):
			dyld_info = struct.unpack_from("<10I", data, offset + 8)
		offset += size

	if dyld_info is None:
		raise ValueError("%s has no dyld bind information" % path)

	_, _, bind_off, bind_size, _, _, lazy_off, lazy_size, _, _ = dyld_info
	totals = [0, 0, 0]
	for start, size in ((bind_off, bind_size), (lazy_off, lazy_size)):
		if size:
			result = walk_binds(data, start, size, patch)
			totals = [a + b for a, b in zip(totals, result)]

	if patch:
		with open(path, "wb") as f:
			f.write(data)
	return totals


def main():
	if len(sys.argv) != 2:
		print("Usage: weaken-macos-imports.py <library.dylib>", file=sys.stderr)
		return 2

	path = sys.argv[1]
	total, already_weak, patched = process(path, patch=True)
	print("%s: %d imports, %d already weak, %d marked weak" % (path, total, already_weak, patched))

	# Verify: re-read and make sure every bound symbol is weak now.
	total, already_weak, _ = process(path, patch=False)
	if total == 0 or already_weak != total:
		print("Verification failed: %d of %d imports are weak" % (already_weak, total), file=sys.stderr)
		return 1
	print("Verified: all %d imports are weak" % total)
	return 0


if __name__ == "__main__":
	sys.exit(main())
