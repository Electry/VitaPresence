#ifndef _MAIN_H_
#define _MAIN_H_

#define VITAPRESENCE_PORT 0xCAFE

#define GET_OFFSET(base, offset) (uint32_t *)((uint32_t)(base) + (offset))
#define APP_LIST_GET_BUBBLEID(base)  GET_OFFSET(base, 0x558)
#define APP_LIST_GET_PID(base)      *GET_OFFSET(base, 0x578)
#define APP_LIST_GET_TITLE(base)     GET_OFFSET(base, 0x62C)
#define APP_LIST_GET_TITLEID(base)   GET_OFFSET(base, 0x900)
#define APP_LIST_GET_STATE(base)    *GET_OFFSET(base, 0xAA0)

#define TITLEID_LEN 10
#define TITLE_LEN   128

typedef enum {
    APP_RUNNING = 2,
    APP_SUSPENDED = 3
} app_state_t;

typedef struct {
    uint32_t magic;
    int index;
    char titleid[TITLEID_LEN];
    char title[TITLE_LEN];
} vitapresence_data_t;

typedef struct {
	int savestate_mode;
	int num;
	unsigned int sp;
	unsigned int ra;

	int pops_mode;
	int draw_psp_screen_in_pops;
	char title[128];
	char titleid[12];
	char filename[256];

	int psp_cmd;
	int vita_cmd;
	int psp_response;
	int vita_response;
} SceAdrenaline;

typedef struct {
	uint32_t magic;
	uint32_t version;
	uint32_t keyTableOffset;
	uint32_t dataTableOffset;
	uint32_t indexTableEntries;
} sfo_header_t;

typedef struct {
	uint16_t keyOffset;
	uint16_t param_fmt;
	uint32_t paramLen;
	uint32_t paramMaxLen;
	uint32_t dataOffset;
} sfo_entry_t;

#endif
