#ifndef _MAIN_H_
#define _MAIN_H_

#define VITAPRESENCE_PORT 0xCAFE

#define GET_OFFSET(base, offset) (uint32_t *)((uint32_t)(base) + (offset))
#define APP_LIST_GET_PID(base)     *GET_OFFSET(base, 0x578)
#define APP_LIST_GET_STATE(base)   *GET_OFFSET(base, 0xAA0)
#define APP_LIST_GET_TITLEID(base)  GET_OFFSET(base, 0x900)
#define APP_LIST_GET_TITLE(base)    GET_OFFSET(base, 0x62C)

typedef enum {
    APP_RUNNING = 2,
    APP_SUSPENDED = 3
} app_state_t;

typedef struct {
    uint32_t magic;
    int index;
    char titleid[10];
    char title[128];
} vitapresence_data_t;

#endif
