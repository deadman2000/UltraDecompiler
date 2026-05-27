/*
 * msdos.h - MS-DOS INT 21h function wrappers for decompiled programs
 *
 * This header is part of the UltraDecompiler project.
 * It provides human-readable, high-level function declarations
 * that the decompiler emits instead of raw intdos() / REGS manipulation
 * when it can recognize specific AH values at INT 21h sites.
 *
 * These functions are semantically equivalent to the corresponding
 * MS-DOS INT 21h services (function numbers in parentheses).
 *
 * Intended for use with code generated for Microsoft QuickC / MSC 5.x / 6.x style.
 */

#ifndef __MSDOS_H
#define __MSDOS_H

#ifdef __cplusplus
extern "C" {
#endif

/* Forward declarations for structures used by some services */
#ifndef _FIND_T_DEFINED
struct find_t {
    char reserved[21];
    char attrib;
    unsigned wr_time;
    unsigned wr_date;
    long size;
    char name[13];
};
#define _FIND_T_DEFINED
#endif

/* Character I/O (AH = 01h..0Ch) */
int  dos_char_input_echo(void);                    /* 01h */
int  dos_char_output(int c);                       /* 02h */
int  dos_char_input(void);                         /* 07h / 08h */
int  dos_print_string(const char far *s);          /* 09h  ($-terminated) */
int  dos_buffered_input(char far *buffer);         /* 0Ah */
int  dos_check_keyboard(void);                     /* 0Bh */

/* Drive and directory functions */
int  dos_get_current_drive(void);                  /* 19h */
void dos_set_current_drive(int drive);             /* 0Eh */
int  dos_get_current_directory(int drive, char far *buffer); /* 47h */
int  dos_set_current_directory(const char far *dir); /* 3Bh */
int  dos_make_directory(const char far *dir);      /* 39h */
int  dos_remove_directory(const char far *dir);    /* 3Ah */

/* File operations (High-level, AH=3Ch..62h) */
int  dos_open(const char far *pathname, int access);           /* 3Dh */
int  dos_creat(const char far *pathname, int attr);            /* 3Ch */
int  dos_close(int handle);                                    /* 3Eh */
int  dos_read(int handle, void far *buffer, unsigned count);   /* 3Fh */
int  dos_write(int handle, const void far *buffer, unsigned count); /* 40h */
int  dos_unlink(const char far *pathname);                     /* 41h */
int  dos_rename(const char far *oldname, const char far *newname); /* 56h */
long dos_lseek(int handle, long offset, int origin);           /* 42h */
int  dos_get_file_size(const char far *pathname, long *size);  /* via 42h+3Dh+3Eh */
int  dos_get_file_attribute(const char far *pathname, unsigned *attr); /* 43h */
int  dos_set_file_attribute(const char far *pathname, unsigned attr);

/* FindFirst / FindNext (AH=4Eh/4Fh) */
int  dos_find_first(const char far *pathname, int attr, struct find_t far *result);
int  dos_find_next(struct find_t far *result);

/* Memory / program control */
void dos_exit(int status);                         /* 4Ch */
int  dos_get_dos_version(void);                    /* 30h */
int  dos_get_free_disk_space(int drive, unsigned *total_clusters,
                             unsigned *avail_clusters,
                             unsigned *sectors_per_cluster,
                             unsigned *bytes_per_sector); /* 36h */

/* Date / Time */
void dos_get_date(unsigned *year, unsigned char *month, unsigned char *day, unsigned char *day_of_week); /* 2Ah */
void dos_set_date(unsigned year, unsigned char month, unsigned char day);
void dos_get_time(unsigned char *hour, unsigned char *minute, unsigned char *second, unsigned char *hundredths); /* 2Ch */
void dos_set_time(unsigned char hour, unsigned char minute, unsigned char second, unsigned char hundredths);

/* DTA handling */
void dos_set_dta(void far *dta);                   /* 1Ah */
void far *dos_get_dta(void);                       /* 2Fh */

/* FCB-based (legacy, rarely used in modern decompilations) */
int  dos_fcb_open(void far *fcb);
int  dos_fcb_close(void far *fcb);

/* Low-level CPU interrupt control (corresponds to CLI/STI) */
void _disable(void);                               /* CLI - disable interrupts (QuickC/MSC style) */
void _enable(void);                                /* STI - enable interrupts (QuickC/MSC style) */

/* Low-level fallback (when AH value is not a compile-time constant) */
int  intdos(void *inregs, void *outregs);          /* from original QuickC <dos.h> */
int  int86(int intno, void *inregs, void *outregs);

#ifdef __cplusplus
}
#endif

#endif /* __MSDOS_H */
