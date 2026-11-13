// Establish the controlling terminal in the spawned process before executing zsh.
#include <signal.h>
#include <stdio.h>
#include <string.h>
#include <sys/ioctl.h>
#include <unistd.h>

int main(int argc, char **argv)
{
	if (getsid(0) != getpid() && setsid() < 0)
	{
		perror("setsid");
		return 126;
	}
	if (ioctl(STDIN_FILENO, TIOCSCTTY, 0) < 0)
	{
		perror("TIOCSCTTY");
		return 126;
	}
	signal(SIGTTOU, SIG_IGN);
	if (tcsetpgrp(STDIN_FILENO, getpgrp()) < 0)
	{
		perror("tcsetpgrp");
		return 126;
	}
	signal(SIGTTOU, SIG_DFL);

	char *plain[] = {"/bin/zsh", "-f", "-i", NULL};
	char *profile[] = {"/bin/zsh", "-i", NULL};
	int load_profile = argc > 1 && strcmp(argv[1], "--profile") == 0;
	execv("/bin/zsh", load_profile ? profile : plain);
	perror("exec zsh");
	return 127;
}
