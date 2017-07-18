#include "logger.h"
#include <stdarg.h>
#include <time.h>
#include <stdio.h>
#include <cctype>

static char timebuf[20];
static struct tm *sTm;
static time_t now;
char *srvrole = "";

static void updTime() {
	now = time(0);
	sTm = gmtime(&now);
	strftime(timebuf, sizeof(timebuf), TIMEFORMAT, sTm);
}

static void printMsg(
	const char *fname,
	int line,
	const char *func,
	MsgType msgType,
	Bool sysErr, 
	int err, 
	const char *format, 
	va_list ap) {
		#define BUFSIZE 500
		char buf[BUFSIZE], errText[BUFSIZE], userMsg[BUFSIZE];
		
		updTime();
		vsnprintf(userMsg, BUFSIZE, format, ap);
		
		if(sysErr)
			snprintf(errText, BUFSIZE, "[%s]", strerror(err));
		else 
			snprintf(errText, BUFSIZE, ":");
		
		snprintf(buf, BUFSIZE, "%s P%d :%d %s | %s%s %s\n", 
			timebuf, getpid(), line,
			srvrole,
			msgType == ERROR ? "ERROR" :
			msgType == LOG ? "LOG": 
			msgType == USAGEERR ? "UsageErr" :
			msgType == FATAL ? "FATAL" : "UNKNOWN",
			errText, userMsg);
		fputs(buf, stderr);
		fflush(stderr);
	}
	


void __errExit(const char *fname, int line, const char *func, const char *format, ...) {
	va_list ap;
	int savedErrno = errno;
	va_start(ap, format);
	printMsg(fname, line, func, ERROR, TRUE, errno, format, ap);
	va_end(ap);
	errno = savedErrno;
	exit(EXIT_FAILURE);
}

void __fatal(const char *fname, int line, const char *func, const char *format, ...) {
	va_list ap;
	va_start(ap, format);
	printMsg(fname, line, func, FATAL, FALSE, 0, format, ap);
	va_end(ap);
	exit(EXIT_FAILURE);
}

void __logger(const char *fname, int line, const char *func, const char *format, ...) {
	va_list ap;
	va_start(ap, format);
	printMsg(fname, line, func, LOG, FALSE, 0, format, ap);
	va_end(ap);
}

void __logpkg(const char *fname, int line, const char *func, const char *buf, int len, int level) {
	__logger(fname, line, func, "(%s %d Bytes):", level == RPACK ? "Recv":"Send", len);

	int len16 = len % 16 == 0 ? len : (len + 16) & (~15);
    
    auto print = [&](FILE* fp) {
        for(int i = 0; i < len16; i++) {
            if(i % 16 == 0) fprintf(fp, "  %04x:  ", i);
            else if(i % 8 == 0) fprintf(fp, "%s ", i < len ? "-":" ");

            if(i < len) fprintf(fp, "%02x ", (unsigned char)buf[i]);
            else fprintf(fp, "   ");

            if(i % 16 == 15) {
                fputc(' ', fp);
                for(int j = i - 15; j <= i; j++) 
                    fputc(j < len ? (isprint(buf[j]) ? buf[j] : '.') : ' ', fp);
                fputc('\n', fp);
            }
        }
    };
    
    print(stderr);
}

// void __log2file(const char *fname, int line, const char *func, const char *format, ...) {
// 	va_list ap;
// 	va_start(ap, format);
// 	printMsg2file(fname, line, func, LOG, FALSE, 0, format, ap);
// 	va_end(ap);
// }

void __usageErr(const char *fname, int line, const char *func, const char *format, ...) {
	va_list ap;
	va_start(ap, format);
	printMsg(fname, line, func, USAGEERR, FALSE, 0, format, ap);
	va_end(ap);
}