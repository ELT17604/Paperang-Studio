#include "PTYBridge.h"
#include <util.h>
#include <spawn.h>
#include <sys/ioctl.h>
#include <sys/wait.h>
#include <fcntl.h>
#include <unistd.h>
#include <signal.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
extern char **environ;
int paper_tty_start(const char *helper,const char *cwd,int cols,int rows,int profile,int *master,int *pid,char *name,size_t capacity) {
 int m=-1,s=-1;char path[128]={0};struct winsize ws={.ws_col=cols,.ws_row=rows};
 if(openpty(&m,&s,path,NULL,&ws)<0)return errno;
 fcntl(m,F_SETFD,FD_CLOEXEC);fcntl(s,F_SETFD,FD_CLOEXEC);
 posix_spawn_file_actions_t actions;posix_spawnattr_t attr;
 posix_spawn_file_actions_init(&actions);posix_spawnattr_init(&attr);
 int rc=posix_spawn_file_actions_addopen(&actions,0,path,O_RDWR,0);
 if(!rc)rc=posix_spawn_file_actions_adddup2(&actions,0,1);
 if(!rc)rc=posix_spawn_file_actions_adddup2(&actions,0,2);
 if(!rc)rc=posix_spawn_file_actions_addchdir_np(&actions,cwd);
 if(!rc)rc=posix_spawnattr_setflags(&attr,POSIX_SPAWN_SETSID|POSIX_SPAWN_CLOEXEC_DEFAULT);
 size_t count=0;while(environ[count])count++;
 char **env=calloc(count+8,sizeof(char*));size_t n=0;
 for(size_t i=0;i<count;i++)if(strncmp(environ[i],"TERM=",5)&&strncmp(environ[i],"LC_ALL=",7)&&strncmp(environ[i],"NO_COLOR=",9)&&strncmp(environ[i],"PAGER=",6)&&strncmp(environ[i],"PS1=",4))env[n++]=environ[i];
 env[n++]="TERM=dumb";env[n++]="LC_ALL=en_US.UTF-8";env[n++]="NO_COLOR=1";env[n++]="PAGER=cat";env[n++]="PS1=paper-tty%# ";env[n]=NULL;
 char *plain[]={(char*)helper,NULL};char *configured[]={(char*)helper,"--profile",NULL};pid_t child=0;
 if(!rc)rc=posix_spawn(&child,helper,&actions,&attr,profile?configured:plain,env);
 close(s);if(!rc)ioctl(m,TIOCSWINSZ,&ws);
 free(env);posix_spawn_file_actions_destroy(&actions);posix_spawnattr_destroy(&attr);
 if(rc){close(m);return rc;}
 fcntl(m,F_SETFL,fcntl(m,F_GETFL)|O_NONBLOCK);*master=m;*pid=(int)child;
 if(capacity){strncpy(name,path,capacity-1);name[capacity-1]=0;}return 0;
}
int paper_tty_resize(int master,int cols,int rows){struct winsize ws={.ws_col=cols,.ws_row=rows};return ioctl(master,TIOCSWINSZ,&ws);}
void paper_tty_stop(int master,int pid){if(master>=0)close(master);if(pid>0){kill(-pid,SIGHUP);kill(pid,SIGHUP);}}
