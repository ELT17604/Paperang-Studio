// Exec-only helper: establishes the controlling terminal after posix_spawn.
#include <sys/ioctl.h>
#include <unistd.h>
#include <signal.h>
#include <stdio.h>
#include <string.h>
int main(int argc,char **argv) {
 if(getsid(0)!=getpid() && setsid()<0){perror("setsid");return 126;}
 if(ioctl(0,TIOCSCTTY,0)<0){perror("TIOCSCTTY");return 126;}
 signal(SIGTTOU,SIG_IGN);
 if(tcsetpgrp(0,getpgrp())<0){perror("tcsetpgrp");return 126;}
 signal(SIGTTOU,SIG_DFL);
 char *plain[]={"/bin/zsh","-f","-i",NULL};char *profile[]={"/bin/zsh","-i",NULL};
 execv("/bin/zsh",argc>1 && !strcmp(argv[1],"--profile") ? profile:plain);perror("exec zsh");return 127;
}
