#pragma description "OMF .LIB (Microsoft QuickC / LINK) - TIS OMF Appendix 2"
#pragma endian little
#pragma base_address 0
#pragma loop_limit 500000

enum OmfRecordType : u8 {
    Theadr          = 0x80,
    Lheadr          = 0x82,
    Coment          = 0x88,
    Modend          = 0x8A,
    Modend32        = 0x8B,
    Lnames          = 0x96,
    Segdef          = 0x98,
    Segdef32        = 0x99,
    Fixupp          = 0x9C,
    Fixupp32        = 0x9D,
    Pubdef          = 0x90,
    Pubdef32        = 0x91,
    Extdef          = 0x8C,
    Extdef32        = 0x8D,
    Ledata          = 0xA0,
    Ledata32        = 0xA1,
    Lidata          = 0xA2,
    Lidata32        = 0xA3,
    LibraryHeader   = 0xF0,
    LibraryEnd      = 0xF1,
    ExtendedDict    = 0xF2
};

fn align_up(u64 value, u64 alignment) {
    return (value + alignment - 1) / alignment * alignment;
};

fn record_payload_size(u16 rec_len) {
    if (rec_len > 0) {
        return rec_len - 1;
    }
    return 0;
};

fn dict_data_end(u8 fflag) {
    u32 end = fflag == 0xFF ? 512 : fflag * 2;
    if (end < 38) {
        end = 38;
    }
    if (end > 512) {
        end = 512;
    }
    return end;
};

fn is_ascii_id_byte(u8 b) {
    if (b >= 0x30 && b <= 0x39) {
        return true;
    }
    if (b >= 0x41 && b <= 0x5A) {
        return true;
    }
    if (b >= 0x61 && b <= 0x7A) {
        return true;
    }
    return b == 0x5F || b == 0x40 || b == 0x3F || b == 0x24;
};

fn is_ascii_identifier(u64 name_addr, u8 name_len) {
    for (u8 i = 0, i < name_len, i += 1) {
        if (!is_ascii_id_byte($[name_addr + i])) {
            return false;
        }
    }
    return true;
};

fn try_symbol_at(u64 base, u32 off, u32 end) {
    if (off < 38 || off >= end) {
        return false;
    }
    u8 name_len = $[base + off];
    if (name_len < 1 || name_len > 255) {
        return false;
    }
    if (off + 1 + name_len + 2 > end) {
        return false;
    }
    return is_ascii_identifier(base + off + 1, name_len);
};

// Поля заголовка F0h по фиксированным смещениям (доступны из любого места pattern)
le u16 omfHeaderRecordLength @ 0x01;
le u32 omfDictionaryOffset @ 0x03;
le u16 omfDictionaryBlockCount @ 0x07;
u8 omfLibraryFlags @ 0x09;

fn omf_page_size() {
    return omfHeaderRecordLength + 3;
};

struct OmfCountedString {
    u8 len;
    char name[len];
};

struct OmfLibraryHeaderPage {
    OmfRecordType type;
    le u16 recordLength;
    le u32 dictionaryOffset;
    le u16 dictionaryBlockCount;
    u8 flags;
    padding[recordLength - 7];
};

struct OmfRecord {
    OmfRecordType type;
    le u16 length;
    if (type == OmfRecordType::Theadr || type == OmfRecordType::Lheadr) {
        OmfCountedString name;
        u8 rest[record_payload_size(length) - 1 - name.len];
    } else {
        u8 bytes[record_payload_size(length)];
    }
    u8 checksum;
};

struct OmfModuleRecordStep {
    OmfRecord record;
    if (record.type == OmfRecordType::Modend || record.type == OmfRecordType::Modend32) {
        break;
    }
};

struct OmfObjectModule {
    OmfModuleRecordStep steps[while($ < omfDictionaryOffset)];
    if ($ < align_up($, omf_page_size()) && align_up($, omf_page_size()) <= omfDictionaryOffset) {
        u8 modulePadding[align_up($, omf_page_size()) - $];
    }
};

struct OmfLibraryModuleSlot {
    OmfRecordType headerRecordType;
    le u16 headerRecordLength;
    OmfCountedString headerName;
    u8 headerRecordRest[record_payload_size(headerRecordLength) - 1 - headerName.len];
    u8 headerRecordChecksum;
    OmfObjectModule moduleBody;
};

struct OmfDictionarySymbol {
    u8 nameLen;
    char name[nameLen];
    le u16 modulePage;
    if (((nameLen + 1 + 2) % 2) != 0) {
        u8 alignmentPad;
    }
};

struct OmfDictionaryLinearEntry {
    if ($ >= parent.dataEnd) {
        break;
    }
    u8 nameLen;
    if (nameLen < 1 || nameLen > 255) {
        break;
    }
    if ($ + 1 + nameLen + 2 > parent.dataEnd) {
        break;
    }
    if (!is_ascii_identifier($ + 1, nameLen)) {
        break;
    }
    OmfDictionarySymbol symbol;
};

struct OmfDictionaryBlock {
    u8 htab[37];
    u8 fflag;
    u32 dataEnd = dict_data_end(fflag);
    u64 base = addressof(htab);

    if (try_symbol_at(base, htab[0] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_00 @ (base + htab[0] * 2); }
    if (try_symbol_at(base, htab[1] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_01 @ (base + htab[1] * 2); }
    if (try_symbol_at(base, htab[2] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_02 @ (base + htab[2] * 2); }
    if (try_symbol_at(base, htab[3] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_03 @ (base + htab[3] * 2); }
    if (try_symbol_at(base, htab[4] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_04 @ (base + htab[4] * 2); }
    if (try_symbol_at(base, htab[5] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_05 @ (base + htab[5] * 2); }
    if (try_symbol_at(base, htab[6] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_06 @ (base + htab[6] * 2); }
    if (try_symbol_at(base, htab[7] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_07 @ (base + htab[7] * 2); }
    if (try_symbol_at(base, htab[8] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_08 @ (base + htab[8] * 2); }
    if (try_symbol_at(base, htab[9] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_09 @ (base + htab[9] * 2); }
    if (try_symbol_at(base, htab[10] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_10 @ (base + htab[10] * 2); }
    if (try_symbol_at(base, htab[11] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_11 @ (base + htab[11] * 2); }
    if (try_symbol_at(base, htab[12] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_12 @ (base + htab[12] * 2); }
    if (try_symbol_at(base, htab[13] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_13 @ (base + htab[13] * 2); }
    if (try_symbol_at(base, htab[14] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_14 @ (base + htab[14] * 2); }
    if (try_symbol_at(base, htab[15] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_15 @ (base + htab[15] * 2); }
    if (try_symbol_at(base, htab[16] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_16 @ (base + htab[16] * 2); }
    if (try_symbol_at(base, htab[17] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_17 @ (base + htab[17] * 2); }
    if (try_symbol_at(base, htab[18] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_18 @ (base + htab[18] * 2); }
    if (try_symbol_at(base, htab[19] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_19 @ (base + htab[19] * 2); }
    if (try_symbol_at(base, htab[20] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_20 @ (base + htab[20] * 2); }
    if (try_symbol_at(base, htab[21] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_21 @ (base + htab[21] * 2); }
    if (try_symbol_at(base, htab[22] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_22 @ (base + htab[22] * 2); }
    if (try_symbol_at(base, htab[23] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_23 @ (base + htab[23] * 2); }
    if (try_symbol_at(base, htab[24] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_24 @ (base + htab[24] * 2); }
    if (try_symbol_at(base, htab[25] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_25 @ (base + htab[25] * 2); }
    if (try_symbol_at(base, htab[26] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_26 @ (base + htab[26] * 2); }
    if (try_symbol_at(base, htab[27] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_27 @ (base + htab[27] * 2); }
    if (try_symbol_at(base, htab[28] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_28 @ (base + htab[28] * 2); }
    if (try_symbol_at(base, htab[29] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_29 @ (base + htab[29] * 2); }
    if (try_symbol_at(base, htab[30] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_30 @ (base + htab[30] * 2); }
    if (try_symbol_at(base, htab[31] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_31 @ (base + htab[31] * 2); }
    if (try_symbol_at(base, htab[32] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_32 @ (base + htab[32] * 2); }
    if (try_symbol_at(base, htab[33] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_33 @ (base + htab[33] * 2); }
    if (try_symbol_at(base, htab[34] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_34 @ (base + htab[34] * 2); }
    if (try_symbol_at(base, htab[35] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_35 @ (base + htab[35] * 2); }
    if (try_symbol_at(base, htab[36] * 2, dataEnd)) { OmfDictionarySymbol sym_htab_36 @ (base + htab[36] * 2); }

    OmfDictionaryLinearEntry linear[while($ < base + dataEnd)];

    if ($ < base + 512) {
        u8 unused[base + 512 - $];
    }
};

struct OmfLibraryEndRecord {
    OmfRecordType type;
    le u16 recordLength;
    u8 data[record_payload_size(recordLength)];
    u8 checksum;
};

struct OmfExtendedDictionaryRecord {
    OmfRecordType type;
    le u16 recordLength;
    le u16 moduleCount;
    u8 data[record_payload_size(recordLength) - 2];
    u8 checksum;
};

struct OmfLibraryFile {
    OmfLibraryHeaderPage header;
    if ($ < omf_page_size()) {
        u8 headerPagePadding[omf_page_size() - $];
    }
    OmfLibraryModuleSlot objectModules[while($ < omfDictionaryOffset)];
    if ($ < omfDictionaryOffset && $ < sizeof($) && $[0] == OmfRecordType::LibraryEnd) {
        OmfLibraryEndRecord libraryEnd;
    }
    OmfDictionaryBlock dictionaryBlocks[omfDictionaryBlockCount] @ omfDictionaryOffset;
    if ($ < sizeof($) && $[0] == OmfRecordType::ExtendedDict) {
        OmfExtendedDictionaryRecord extendedDictionary;
    }
};

OmfLibraryFile library @ 0x00;
