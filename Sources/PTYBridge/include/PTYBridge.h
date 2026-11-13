#ifndef PTY_BRIDGE_H
#define PTY_BRIDGE_H
#include <stddef.h>
int paper_tty_start(const char *helper, const char *cwd, int cols, int rows, int profile, int *master, int *pid,
                    char *name, size_t capacity);
int paper_tty_resize(int master, int cols, int rows);
void paper_tty_stop(int master, int pid);
#endif
