#include <stdint.h>
#include <arpa/inet.h>
uint8_t  hton(uint8_t  u) { return u; }
uint16_t hton(uint16_t u) { return htons(u); }
uint32_t hton(uint32_t u) { return htonl(u); }

uint8_t  ntoh(uint8_t  u) { return u; }
uint16_t ntoh(uint16_t u) { return ntohs(u); }
uint32_t ntoh(uint32_t u) { return ntohl(u); }

uint8_t  pntoh8 (const char* p) {	return *(uint8_t*)p; 			}
uint16_t pntoh16(const char* p) {	return ntohs(*(uint16_t*)p); 	}
uint32_t pntoh32(const char* p) {	return ntohl(*(uint32_t*)p); 	}
int htopn8 (char* p, uint8_t  v) { *p = v; return 0; }
int htopn16(char *p, uint16_t v) { *(uint16_t*) p = htons(v); return 0; }
int htopn32(char *p, uint32_t v) { *(uint32_t*) p = htonl(v); return 0; }
