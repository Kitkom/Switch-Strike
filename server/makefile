CC = g++
CFLAGS = --std=c++11 -g -Wall

all: server

%.o: %.c
	$(CC) -c $< -o $@ $(CFLAGS)

%.o: %.cpp
	$(CC) -c $< -o $@ $(CFLAGS)
    
server: main.o logger.o common.o
	$(CC) $^ -o $@ $(CFLAGS) `mysql_config --libs` 
	
.PHONY: clean

clean: 
	rm -f *.o ts