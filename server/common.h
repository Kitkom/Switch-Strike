#pragma once
#include <string> 
#include <string.h>
#include <assert.h>
#include <stdint.h>
#include <functional>
#include "tinyformat.h"

#define CUR_VER 1
#define CLIRECN 1200
#define BUF_LEN 1024 * 10
#define SQL_LEN 1024 * 10
#define BACKLOG 1000
#define N 4

#define Ref std::reference_wrapper

/* ==== */
uint8_t  ntoh(uint8_t  u);
uint16_t ntoh(uint16_t u);
uint32_t ntoh(uint32_t u);
uint8_t pntoh8(const char*);
uint16_t pntoh16(const char*);
uint32_t pntoh32(const char*);
int htopn8(char*,  uint8_t);
int htopn16(char*, uint16_t);
int htopn32(char*, uint32_t);
/* ==== */

struct Card {
    uint8_t color;
    uint8_t value;
    std::string toString() const {
        return tfm::format("Card[%s%d]", color ? "B" : "W", value);
    }
};

#define Assert(cond) do { if(!(cond)) return -1; } while(0)

struct Player {
    Card cards[N];
    uint8_t hp;
    void gen() { /* randomly generate my cards */
        hp = 20;
        for(int i = 0; i < N; i++) {
            bool has;
            do {
                cards[i].color = rand() % 2;
                cards[i].value = rand() % 2 + 1;
                has = false;
                for(int j = 0; j < i; j++)
                    if(cards[i].color == cards[j].color && cards[i].value == cards[j].value) {
                        has = true;
                        break;
                    }
            } while(has);
        }
    }
    int parse(const char *buf) { /* return 0 => ok */
        Assert((uint8_t)buf[0] == 0x91);
        Assert((uint8_t)buf[1] == 0x10);
        assert(pntoh16(buf+2) == 9);
        memcpy(cards, buf+4, sizeof(cards));
// rule 0.0.2

//for (int i = 0; i < N; ++i)
//    if (cards[i].value == 2)
//        cards[i].value = 5; 
        hp = buf[12];
        return 0;
    }
    int serialize(char *buf) const { 
        buf[0] = 0x11;
        buf[1] = 0x10;
        htopn16(buf+2, 9);
        memcpy(buf+4, cards, sizeof(cards));
        buf[12] = hp;
        return 9 + 4;
    }
    std::string toString() const {
        return tfm::format("Player[%s, %s, %s, %s, hp=%d]"
        , cards[0].toString(), cards[1].toString()
        , cards[2].toString(), cards[3].toString()
        , hp);
    }
};

/* 确认两人匹配后，使用 
 * fork(); ... 
 * round(mysql, user1, fd1, user2, fd2)
 * 处理从 Preparation 之后的网络、日志和数据库任务
 */

struct User {
    uint16_t uid;
    std::string username;
    User(uint16_t uid, std::string username): uid(uid), username(username) {}
};

struct Message {
    uint8_t type;
    std::string msg;
    Message(uint8_t type, std::string msg): type(type), msg(msg) {}
    int serialize(char *buf) const { /* return all_len */
        uint16_t len = msg.size() + 1; /* not to forget trailing zero */
        buf[0] = 0x11;
        buf[1] = type;
        htopn16(buf+2, len);
        memcpy(buf+4, msg.c_str(), msg.size()+1);
        return 4 + len;
    }
};

struct Result: public Message {
    Result(std::string msg): Message(0x13, msg) {}
};

struct Action {
    uint8_t a, b;
    uint8_t attack[4];
    int parse(const char *buf) {        
        Assert((uint8_t)buf[0] == 0x91);
        Assert((uint8_t)buf[1] == 0x11);
        Assert(pntoh16(buf+2) == 6);
        a = buf[4]; b = buf[5];
        attack[0] = buf[6];
        attack[1] = buf[7];
        attack[2] = buf[8];
        attack[3] = buf[9];
        return 0;
    }
    int serialize(char *buf, uint8_t op_hp, uint8_t your_hp) const {
        buf[0] = 0x11; buf[1] = 0x11;
        htopn16(buf+2, 8);
        buf[4] = a;
        buf[5] = b;
        buf[6] = attack[0];
        buf[7] = attack[1];
        buf[8] = attack[2];
        buf[9] = attack[3];
        buf[10] = op_hp;
        buf[11] = your_hp;
        return 12;
    }
    std::string toString() const {
        return tfm::format(
        "Action[replace[%d<->%d], attack[%d, %d, %d, %d]"
        , a, b
        , attack[0], attack[1], attack[2], attack[3]);
    }
};

struct Header {
    const static int PKG_LEN = 4;
    uint8_t dir;
    uint8_t type;
    uint16_t data_len;
    int parse(const char *buf) {
        dir = buf[0];
        type = buf[1];
        data_len = pntoh16(buf+2);
        return 0;
    }
};

struct Version {
    uint32_t ver;
    Version(uint32_t ver) : ver(ver) {}
    int serialize(char *buf) const {
        buf[0] = 0x11;
        buf[1] = 0x01;
        htopn16(buf+2, 4);
        htopn32(buf+4, ver);
        return 8;
    }
};

typedef struct sockaddr_in SA4;
typedef struct sockaddr SA;

typedef struct Clirec {
    /* 客户端在哪个状态  */
	uint8_t dir, stage; 
    int fd;
    uint16_t uid;
    bool verified;
    bool success;
    Clirec *matched;
    std::string info;
    std::string username;
    std::string password;
    Clirec() { dir = 0x11; stage = 0x01; fd = -1; }
    void reset() { dir = 0x11; stage = 0x01; fd = -1; }
} Clirec;
