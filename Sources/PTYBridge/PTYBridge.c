#include "PTYBridge.h"

#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <spawn.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <util.h>

extern char **environ;

static char **terminal_environment(void)
{
	size_t count = 0;
	while (environ[count])
	{
		count++;
	}

	char **environment = calloc(count + 6, sizeof(*environment));
	if (!environment)
	{
		return NULL;
	}

	size_t next = 0;
	for (size_t i = 0; i < count; i++)
	{
		char *entry = environ[i];
		if (strncmp(entry, "TERM=", 5) && strncmp(entry, "LC_ALL=", 7) && strncmp(entry, "NO_COLOR=", 9) &&
		    strncmp(entry, "PAGER=", 6) && strncmp(entry, "PS1=", 4))
		{
			environment[next++] = entry;
		}
	}
	environment[next++] = "TERM=dumb";
	environment[next++] = "LC_ALL=en_US.UTF-8";
	environment[next++] = "NO_COLOR=1";
	environment[next++] = "PAGER=cat";
	environment[next++] = "PS1=paper-tty%# ";
	environment[next] = NULL;
	return environment;
}

int paper_tty_start(const char *helper, const char *cwd, int cols, int rows, int profile, int *master, int *pid,
                    char *name, size_t capacity)
{
	int master_fd = -1;
	int slave_fd = -1;
	char path[128] = {0};
	struct winsize size = {.ws_col = cols, .ws_row = rows};
	if (openpty(&master_fd, &slave_fd, path, NULL, &size) < 0)
	{
		return errno;
	}

	fcntl(master_fd, F_SETFD, FD_CLOEXEC);
	fcntl(slave_fd, F_SETFD, FD_CLOEXEC);

	posix_spawn_file_actions_t actions;
	posix_spawnattr_t attributes;
	int result = posix_spawn_file_actions_init(&actions);
	if (result)
	{
		goto close_pty;
	}
	result = posix_spawnattr_init(&attributes);
	if (result)
	{
		goto destroy_actions;
	}

	result = posix_spawn_file_actions_addopen(&actions, STDIN_FILENO, path, O_RDWR, 0);
	if (!result)
		result = posix_spawn_file_actions_adddup2(&actions, STDIN_FILENO, STDOUT_FILENO);
	if (!result)
		result = posix_spawn_file_actions_adddup2(&actions, STDIN_FILENO, STDERR_FILENO);
	if (!result)
		result = posix_spawn_file_actions_addchdir_np(&actions, cwd);
	if (!result)
		result = posix_spawnattr_setflags(&attributes, POSIX_SPAWN_SETSID | POSIX_SPAWN_CLOEXEC_DEFAULT);

	pid_t child = 0;
	if (!result)
	{
		char **environment = terminal_environment();
		if (environment)
		{
			char *plain[] = {(char *)helper, NULL};
			char *configured[] = {(char *)helper, "--profile", NULL};
			result = posix_spawn(&child, helper, &actions, &attributes, profile ? configured : plain, environment);
			free(environment);
		}
		else
		{
			result = ENOMEM;
		}
	}

	posix_spawnattr_destroy(&attributes);
	if (!result)
	{
		close(slave_fd);
		ioctl(master_fd, TIOCSWINSZ, &size);
		posix_spawn_file_actions_destroy(&actions);
		fcntl(master_fd, F_SETFL, fcntl(master_fd, F_GETFL) | O_NONBLOCK);
		*master = master_fd;
		*pid = (int)child;
		if (capacity)
		{
			strncpy(name, path, capacity - 1);
			name[capacity - 1] = 0;
		}
		return 0;
	}

destroy_actions:
	posix_spawn_file_actions_destroy(&actions);
close_pty:
	close(slave_fd);
	close(master_fd);
	return result;
}

int paper_tty_resize(int master, int cols, int rows)
{
	struct winsize size = {.ws_col = cols, .ws_row = rows};
	return ioctl(master, TIOCSWINSZ, &size);
}

void paper_tty_stop(int master, int pid)
{
	if (master >= 0)
	{
		close(master);
	}
	if (pid > 0)
	{
		kill(-pid, SIGHUP);
		kill(pid, SIGHUP);
	}
}
