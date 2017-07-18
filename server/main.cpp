#include "common.h"
#include "logger.h"
#include <arpa/inet.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <time.h>
#include <iostream>
#include <cstring>
#include <fstream>
#include <sstream>
#include <unistd.h>
#include <mysql/mysql.h>
#include <cstdlib>
#include <sys/types.h>
#include <fcntl.h>
#include <sys/shm.h>
#include <signal.h>
#include <vector>
#include <set>
#include <functional>
#include <algorithm>
#include <errno.h>
using namespace std;

#define PORT 52288

MYSQL *mysql;

/* ===== */

int exec_action(const Action ac[2], Player pl[2]) {
    swap(pl[0].cards[ac[0].a], pl[0].cards[ac[0].b]);
    swap(pl[1].cards[ac[1].a], pl[1].cards[ac[1].b]);
    int dec[2] = {0}; 
    for(int i = 0; i < 4; i++) {
        if(pl[0].cards[ac[1].attack[i]].color != pl[1].cards[i].color)
            dec[0] += pl[1].cards[i].value;
        if(pl[1].cards[ac[0].attack[i]].color != pl[0].cards[i].color)
            dec[1] += pl[0].cards[i].value;
    }
    
    pl[0].hp = dec[0] > pl[0].hp ? 0 : pl[0].hp - dec[0];
    pl[1].hp = dec[1] > pl[1].hp ? 0 : pl[1].hp - dec[1];
    
    return 0;
}

int game_persist(MYSQL *con, const User &u1, const Player &p1, const User &u2, const Player &p2) {
    /* TODO */
    return 0;
}

int Recv(int fd, char *buf, int len, int flags) {
    int r = recv(fd, buf, len, flags);
    log_rpack(buf, len);
    return r;
}

int Write(int fd, char *buf, int len) {
    log_spack(buf, len);
    return write(fd, buf, len);
}

int round(MYSQL *con, const User &user1, int fd1, const User &user2, int fd2) {
    static char buf[1024*10];
    int mfd = 1 + (fd1 > fd2 ? fd1 : fd2);
    Player pl[2];
    
    enum {ERR = 0, WIN = 1, LOST = 2, DRAW = 3};
    
    auto except = [&](int fd, int type = 0, bool toexit = true) {
        int n;
        if(type == 0) {
            n = Result("Opponent gave up").serialize(buf);
        }
        else if(type == 1) {
            n = Result("You win").serialize(buf);
        }
        else if(type == 2) {
            n = Result("You lost").serialize(buf);
        }
        else if(type == 3) {
            n = Result("Draw").serialize(buf);
        }
        Write(fd, buf, n);            
        if(toexit) {
            logger("Exiting round");
            exit(0);
        }
        else return 0;
    };
    
    fd_set rset;
    
    /* preparation */
    {
        /* Recv */
        int n;
        bool ok1 = 0, ok2 = 0;
        while(!(ok1 && ok2)) {
            FD_ZERO(&rset);
            if(!ok1) FD_SET(fd1, &rset);
            if(!ok2) FD_SET(fd2, &rset);
            
            int nready = select(mfd, &rset, NULL, NULL, NULL), n;
            assert(nready >= 0);
            if(FD_ISSET(fd1, &rset)) {
                n = Recv(fd1, buf, 9+4, MSG_WAITALL);
                Assert(n >= 0);
                if(n == 0) break;
                ok1 = pl[0].parse(buf) == 0;
                if(!ok1) logger("Parse failed");
                logger("Receive preparation[uid=%d, %s]", user1.uid, pl[0].toString().c_str());
            }
            if(FD_ISSET(fd2, &rset)) {
                n = Recv(fd2, buf, 9+4, MSG_WAITALL);
                Assert(n > 0);
                if(n == 0) break;
                ok2 = pl[1].parse(buf) == 0;
                
                logger("Receive preparation[uid=%d, %s]", user2.uid, pl[1].toString().c_str());
            }
        }
        
        /* Write */
        if(ok1 && !ok2) except(fd1);
        
        if(!ok1 && ok2) except(fd2);
        
        if(!(ok1 && ok2)) return 0;
        
        n = pl[0].serialize(buf);
        n = Write(fd2, buf, n);
        if(n <= 0) except(fd2);
        
        n = pl[1].serialize(buf);
        n = Write(fd1, buf, n);
        if(n <= 0) except(fd1);
        
        logger("Sent preparation");
    }
    
    game_persist(con, user1, pl[0], user2, pl[1]);
    
    /* action */
    int step = 0;
    while(1) {
        bool ok1 = 0, ok2 = 0;
        Action ac[2];
        int n;
        while(!(ok1 && ok2)) {
            FD_ZERO(&rset);
            if(!ok1) FD_SET(fd1, &rset);
            if(!ok2) FD_SET(fd2, &rset);
            
            int nready = select(mfd, &rset, NULL, NULL, NULL);
            assert(nready >= 0);
            if(FD_ISSET(fd1, &rset)) {
                n = Recv(fd1, buf, 10, MSG_WAITALL);
                Assert(n >= 0);
                if(n == 0) break;
                ok1 = ac[0].parse(buf) == 0;
                if(!ok1) logger("Parse error");
                logger("Receive action[uid=%d, %s]", user1.uid, ac[0].toString().c_str());
            }
            if(FD_ISSET(fd2, &rset)) {
                n = Recv(fd2, buf, 10, MSG_WAITALL);
                Assert(n > 0);
                if(n == 0) break;
                ok2 = ac[1].parse(buf) == 0;
                if(!ok2) logger("Parse error");
                logger("Receive action[uid=%d, %s]", user2.uid, ac[1].toString().c_str());
            }
        }
        
        if(ok1 && !ok2) except(fd1);
    
        if(!ok1 && ok2) except(fd2);
        
        if(!(ok1 && ok2)) return 0;
        
        exec_action(ac, pl);
        game_persist(con, user1, pl[0], user2, pl[1]);
        
        logger("After exec player1=%s, player2=%s"
            , pl[0].toString().c_str()
            , pl[1].toString().c_str());
        
        n = ac[1].serialize(buf, pl[1].hp, pl[0].hp);
        n = Write(fd1, buf, n);
        if(n <= 0) except(fd2);
        
        n = ac[0].serialize(buf, pl[0].hp, pl[1].hp);
        n = Write(fd2, buf, n);
        if(n <= 0) except(fd1);
        
        if(pl[0].hp == 0 && pl[1].hp > 0) {
            logger("user2[uid=%d] win", user2.uid);
            except(fd1, LOST, false);
            except(fd2, WIN, true);
        }
        
        if(pl[0].hp > 0 && pl[1].hp == 0) {
            logger("user1[uid=%d] win", user1.uid);
            except(fd1, WIN, false);
            except(fd2, LOST, true);
        }
        
        if(pl[0].hp == 0 && pl[1].hp == 0) {
            logger("Game draw");
            except(fd1, DRAW, false);
            except(fd2, DRAW, true);
        }
        
        step++;
    }
}
/* ====== */

void initDB() {
	if ((mysql = mysql_init(NULL))==NULL) {
    	errExit("mysql_init failed");
    	}
	
	/***IMPORTANT***/
	/* Enable reconnection & timout */
	/* mariadb mysql_query() will block in high query flow(bug?) */
	/* resulting missing Write query & blocking server child process */
	unsigned int timeout= 5;
	mysql_options(mysql, MYSQL_OPT_RECONNECT, (void *)"1"); /* enable */
	mysql_options(mysql, MYSQL_OPT_READ_TIMEOUT, (void *)&timeout);
	
    /* 连接数据库，失败返回NULL
       1、mysqld没运行
       2、没有指定名称的数据库存在 */
    if (mysql_real_connect(mysql,"localhost","root", "root123","game",0, NULL, 0)==NULL) {
    	fprintf(stderr, "mysql_real_connect failed(%s)\n", mysql_error(mysql));
    	exit(-1);
    	}

    /* 设置字符集，否则读出的字符乱码，即使/etc/my.cnf中设置也不行 */
    mysql_set_character_set(mysql, "gbk"); 
}

struct Playing {
    pid_t pid;
    Ref<Clirec> p1, p2;
    Playing(pid_t pid, Clirec &a, Clirec &b): pid(pid), p1(a), p2(b) {}
};

vector<Playing> playing;
vector<Ref<Clirec>> recovered;

void __chldext_cb(int __u) {
    pid_t pid;
	while((pid = waitpid(-1, NULL, WNOHANG)) > 0) {
        for(int i = 0; i < playing.size();) {
            if(playing[i].pid == pid) {
                recovered.push_back(playing[i].p1);
                recovered.push_back(playing[i].p2);
                playing.erase(playing.begin()+i);
            }
            else
                i++;
        }
    }
}

bool regist(MYSQL *con, Clirec &c) {
    static char sqlbuf[SQL_LEN];
	snprintf(sqlbuf
			, SQL_LEN
			, "insert into user(username, password) values('%s', '%s')"
			, c.username.c_str(), c.password.c_str());
	
	if(mysql_query(con, sqlbuf)) {
        /* TODO: duplicate username */
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = "Username exists";
        return false;
	}
    
    snprintf(sqlbuf, SQL_LEN, "select uid from user where username='%s'", c.username.c_str());
    if(mysql_query(con, sqlbuf)) {
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = mysql_error(con);
        return false;
    }
    
    /* fetch uid */
    MYSQL_RES *result = mysql_store_result(con);
    
    if (result == NULL) 
    {
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = mysql_error(con);
        return false;
    }

    string r;
    MYSQL_ROW row;
    if(row = mysql_fetch_row(result)) {
        r = row[0];
        mysql_free_result(result);
    }
    
    c.uid = atoi(r.c_str());
    c.verified = true;
    c.info = "Register success";
        
	return true;
}

bool verify(MYSQL *con, Clirec &c) {
    static char sqlbuf[SQL_LEN];
	snprintf(sqlbuf
			, SQL_LEN
			, "select uid, password from user where username = '%s'"
			, c.username.c_str());
	
	if(mysql_query(con, sqlbuf)) {
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = "Username not exists";
        return false;
	}
    
    /* fetch uid */
    MYSQL_RES *result = mysql_store_result(con);
    
    if (result == NULL) 
    {
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = "Username not exists";
        return false;
    }

    string passwd, uid;
    MYSQL_ROW row;
    if(row = mysql_fetch_row(result)) {
        uid = row[0]; passwd = row[1];
        if(passwd != c.password) {
            c.verified = false;
            c.info = "Wrong password";
            return false;
        }
        c.uid = atoi(row[0]); 
        c.verified = true;
        c.info = "Login success";
        mysql_free_result(result);
    }
    else {
        logger("SQL errno=%d, errstr=%s", mysql_errno(con), mysql_error(con));
        c.verified = false;
        c.info = "Username not exists";
        return false;
    }
    
	return true;
}

void server(uint16_t port) {	
	MYSQL* con = mysql;
    
	/* buffer */
	static Clirec clirec[CLIRECN];
	static char buf[BUF_LEN];
	ssize_t n;
	
	/* connection fd */
	int listenfd, connfd;
	socklen_t clilen;
	SA4 srvaddr, cliaddr;
	
	/* set srvaddr */
	bzero(&srvaddr, sizeof(srvaddr));
	srvaddr.sin_family = AF_INET;
	srvaddr.sin_addr.s_addr = htonl(INADDR_ANY);
	srvaddr.sin_port = htons(port);
	
	/* listen & bind */
	if((listenfd = socket(AF_INET, SOCK_STREAM, 0)) < 0) {
		errExit("Fail to create socket");
	}
	
    int optval = 1;
    if (setsockopt(listenfd, SOL_SOCKET, SO_REUSEADDR, &optval, sizeof(optval)) == -1)
            errExit("setsockopt");
    
	if(bind(listenfd, (const SA*) &srvaddr, sizeof(srvaddr)) < 0) {
		errExit("Fail to bind socket");
	}
	
	listen(listenfd, BACKLOG);

	/* select fd_set */
	int nready, maxfd = listenfd, maxi = -1, i;
	fd_set rset, wset, _rset, _wset;
	FD_ZERO(&rset); FD_ZERO(&wset);
	FD_SET(listenfd, &rset);
    set<Clirec*> waiting_players;
	
    auto close_cli = [&](Clirec &cli) {
        FD_CLR(cli.fd, &wset);
        FD_CLR(cli.fd, &rset);
        close(cli.fd);
        cli.reset();
    };
    
    auto change_stage = [&](Clirec &cli, int dir, int stage) {
        //waiting_players.erase(&cli);
        cli.dir = dir; cli.stage = stage;
        FD_CLR(cli.fd, &wset); FD_CLR(cli.fd, &rset);
        if(dir != 0) /* dir == 0 => mute cli.fd */
            FD_SET(cli.fd, (dir == 0x11? &wset : &rset));
    };
    
    auto err_handler = [&](int n, Clirec &c) {
        if(n == 0) {
            logger("return 0");
            close_cli(c);
            return 1;
        }
        if(n < 0) {
            logger("return negative");
            close_cli(c);
            return 1;
        }
        return 0;
    };
    
	for(;;) {
        if(!recovered.empty()) {
            for(auto &cli: recovered) {
                change_stage(cli, 0x91, 0x04);
            }
            recovered.clear();
        }
        
		_rset = rset; _wset = wset;
		nready = select(maxfd+1, &_rset, &_wset, NULL, NULL);
		
		if(nready < 0) {
            if(errno == EINTR) continue;
			errExit("Fail to select");
        }
		
		if(nready == 0)
			errExit("Select timeout");
		
		/* new connection */
		if(FD_ISSET(listenfd, &_rset)) {
			clilen = sizeof(cliaddr);
			if((connfd = accept(listenfd, (SA*) &cliaddr, &clilen)) < 0)
				errExit("Accept failed");		
			
			for(i = 0; i < CLIRECN; i++) /* find a place in clirec */
				if(clirec[i].fd == -1) {
					clirec[i].fd = connfd;
					break;
				}
			
			if(i == CLIRECN) { /* no room */
				fprintf(stderr, "No room for new connection\n");
				close(connfd);
			}
			else {
				FD_SET(connfd, &wset); 
				maxfd = max(maxfd, connfd);
				maxi = max(maxi, i);
				fprintf(stderr, " S - new connection from port %d assigned fd %d \n", cliaddr.sin_port, i);
			}
			
			if(--nready <= 0)
				continue;
		}
		
		for(i = 0; i <= maxi && nready > 0; i++) {
			Clirec &c = clirec[i];
			if(c.fd < 0) continue;
            
			if(FD_ISSET(c.fd, &_rset)) {
				assert(c.dir == 0x91);
                
                Header header;
                n = Recv(c.fd, buf, header.PKG_LEN, MSG_WAITALL);
                if(err_handler(n, c)) goto READ_END;
                
                header.parse(buf);
                assert(header.dir == 0x91);
                if(header.type == 0x02) {
                    /* regist request */
                    assert(c.stage == 0x02 || c.stage == 0x03);
                    n = Recv(c.fd, buf + header.PKG_LEN, header.data_len, MSG_WAITALL);
                    
                    uint8_t username_len = buf[header.PKG_LEN];
                    uint8_t passwd_len = buf[header.PKG_LEN+1+username_len];
                    char str[1000] = {0};
                    memcpy(str, buf+Header::PKG_LEN+1, username_len);
                    c.username = str;
                    memset(str, 0, sizeof(str));
                    memcpy(str, buf+Header::PKG_LEN+2+username_len, passwd_len);
                    c.password = str;
                    
                    c.verified = regist(con, c);
                    
                    if(c.verified) {
                        logger("Registered sucess uid=%d, username=%s, password=%s", c.uid , c.username.c_str(), c.password.c_str());
                    }
                    else {
                        logger("Registered failed uid=%d, username=%s, password=%s", c.uid , c.username.c_str(), c.password.c_str());
                    } 
                    
                    change_stage(c, 0x11, 0x02);
                }
                else if(header.type == 0x03) {
                    /* login request */
                    assert(c.stage == 0x02 || c.stage == 0x03);
                    n = Recv(c.fd, buf + header.PKG_LEN, header.data_len, MSG_WAITALL);
                    
                    uint8_t username_len = buf[header.PKG_LEN];
                    uint8_t passwd_len = buf[header.PKG_LEN+1+username_len];
                    char str[1000] = {0};
                    memcpy(str, buf+Header::PKG_LEN+1, username_len);
                    c.username = str;
                    memset(str, 0, sizeof(str));
                    memcpy(str, buf+Header::PKG_LEN+2+username_len, passwd_len);
                    c.password = str;
                    
                    c.verified = verify(con, c);
                    
                     if(c.verified) {
                        logger("Login sucess uid=%d, username=%s, password=%s", c.uid , c.username.c_str(), c.password.c_str());
                    }
                    else {
                        logger("Login failed uid=%d, username=%s, password=%s", c.uid , c.username.c_str(), c.password.c_str());
                    } 
                    
                    change_stage(c, 0x11, 0x03);
                }
                else if(header.type == 0x04) {
                    assert(c.stage == 0x04 || c.stage == 0x05 || c.stage == 0x06 || c.stage == 0x07 || c.stage == 0x08);
                    /* History List, TODO */
                }
                else if(header.type == 0x05) {
                    assert(c.stage == 0x04 || c.stage == 0x05 || c.stage == 0x06 || c.stage == 0x07 || c.stage == 0x08);
                    /* History, TODO */
                }
                else if(header.type == 0x06) {
                    assert(c.stage == 0x04 || c.stage == 0x05 || c.stage == 0x06 || c.stage == 0x07 || c.stage == 0x08);
                    /* Player list request */
                    change_stage(c, 0x11, 0x06);
                }
                else if(header.type == 0x07) {
                    assert(c.stage == 0x04 || c.stage == 0x05 || c.stage == 0x06 || c.stage == 0x07 || c.stage == 0x08);
                    /* Set wait */
                    uint8_t start;
                    n = Recv(c.fd, (char*)&start, 1, MSG_WAITALL);
                    if(err_handler(n, c)) goto READ_END;
                    if(start == 1) {
                        waiting_players.insert(&c);
                        change_stage(c, 0x91, 0x07);
                        logger("User[uid=%d] set waiting=1, waiting[len=%d]", c.uid, waiting_players.size());
                    }
                    else {
                        waiting_players.erase(&c);
                        change_stage(c, 0x91, 0x07);
                        logger("User[uid=%d] set waiting=0, waiting[len=%d]", c.uid, waiting_players.size());
                    }
                }
                else if(header.type == 0x08) {
                    /* select player request */
                    assert(c.stage == 0x04 || c.stage == 0x05 || c.stage == 0x06 || c.stage == 0x07 || c.stage == 0x08);
                    uint16_t uid;
                    n = Recv(c.fd, (char*)&uid, sizeof(uid), MSG_WAITALL);
                    if(err_handler(n, c)) goto READ_END;
                    uid = ntoh(uid);
                    
                    bool found = false;
                    for(auto &r: waiting_players) {
                        if(r->uid == uid) {
                            found = true;
                            c.matched = r;
                            break;
                        }
                    }
                    
                    if(found) {
                        logger("User[uid=%d] select user[uid=%d] sucess", c.uid, uid);
                        c.success = true;
                        change_stage(c, 0x11, 0x08);
                    }
                    else {
                        logger("User[uid=%d] select user[uid=%d] failed", c.uid, uid);
                        c.success = false;
                        change_stage(c, 0x11, 0x08);
                    }
                }
READ_END:                
                if(--nready < 0) break;
			}
            
			
			if(FD_ISSET(c.fd, &_wset)) {
				assert(c.dir == 0x11);
                if(c.stage == 0x01) {
                    /* Version response */
                    n = Version(CUR_VER).serialize(buf);
                    n = Write(c.fd, buf, n);
                    if(err_handler(n, c)) goto WRITE_END;
                    change_stage(c, 0x91, 0x02);
                }
                else if(c.stage == 0x02) {
                    /* Register Response */
                    n = Message(0x02, c.info).serialize(buf);
                    n = Write(c.fd, buf, n);
                    if(err_handler(n, c)) goto WRITE_END;
                    change_stage(c, 0x91, 0x02);
                }
                else if(c.stage == 0x03) {
                    /* Login Response */
                    buf[0] = 0x11; buf[1] = 0x03; buf[2] = 0; buf[3] = 1; buf[4] = c.verified;
                    n = 5;
                    n = Write(c.fd, buf, n);
                    if(err_handler(n, c)) goto WRITE_END;
                    if(c.verified)                        
                        change_stage(c, 0x91, 0x04);
                    else 
                        change_stage(c, 0x91, 0x02);
                }
                else if(c.stage == 0x04) {
                    /* History List, TODO */
                }
                else if(c.stage == 0x05) {
                    /* History, TODO */
                }
                else if(c.stage == 0x06) {
                    /* Player list response */
                    logger("Waiting players list[len=%d] request from User[uid=%d]:", waiting_players.size(), c.uid);
                    int data_len = 0;
                    buf[0] = 0x11; buf[1] = 0x06;
                    for(auto &r: waiting_players) {
                        htopn16(buf+4+data_len, r->uid);
                        data_len += 2;
                        buf[4+data_len] = r->username.size()+1;
                        data_len += 1;
                        strcpy(buf+4+data_len, r->username.c_str());
                        data_len += r->username.size() + 1;
                        fprintf(stderr, "User[uid=%d, username=%s]", r->uid, r->username.c_str());
                    }
                    htopn16(buf+2, data_len);
                    
                    n = data_len + 4;
                    n = Write(c.fd, buf, n);
                    if(err_handler(n, c)) goto WRITE_END;
                    change_stage(c, 0x91, 0x04);
                }
                else if(c.stage == 0x08) {
                    /* Select player response */
                    buf[0] = 0x11; buf[1] = 0x08;
                    htopn16(buf+2, 1); buf[4] = c.success;
                    n = 5;
                    n = Write(c.fd, buf, n);
                    if(err_handler(n, c)) goto WRITE_END;
                    
                    if(c.success) {
                        /* successfully match a game */
                        logger("Matched round[uid1=%d, uid2=%d]", c.uid, c.matched->uid);
                        
                        /* remove from FD_SET */
                        change_stage(c, 0, 0);
                        change_stage(*c.matched, 0, 0);
                        
                        /* remove user from waiting list */
                        waiting_players.erase(&c);
                        waiting_players.erase(c.matched);
                        
                        /* Send matched info to both side */
                        buf[0] = 0x11; buf[1] = 0x09; 
                        htopn16(buf+2, c.matched->username.size()+1);
                        strcpy(buf+4, c.matched->username.c_str());
                        n = c.matched->username.size()+1+4;
                        n = Write(c.fd, buf, n);
                        if(err_handler(n, c)) goto WRITE_END;
                        
                        htopn16(buf+2, c.username.size()+1);
                        strcpy(buf+4, c.username.c_str());
                        n = c.username.size()+1+4;
                        n = Write(c.matched->fd, buf, n);
                        if(err_handler(n, *c.matched)) goto WRITE_END;
                        
                        /* add handler */
                        switch(int pid = fork()) {
                            case 0: /* child */
                                round(con, User(c.uid, c.username), c.fd
                                        , User(c.matched->uid, c.matched->username), c.matched->fd);
                                exit(0);
                            case -1:
                                errExit("fork");
                            default: /* parent */
                                playing.push_back(Playing(pid, c, *c.matched));
                                break;
                        }
                    }
                    else 
                        change_stage(c, 0x91, 0x08);
                }
WRITE_END:
                if(--nready < 0) break;
			}
		}
	}
}

int main() {
    signal(SIGCHLD, __chldext_cb); 
    initDB();
    
	server((uint16_t)PORT);
	
	mysql_close(mysql);
}
