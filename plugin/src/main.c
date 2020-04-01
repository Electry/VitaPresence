#include <vitasdkkern.h>
#include <taihen.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

#include "main.h"

int module_get_offset(SceUID pid, SceUID modid, int segidx, size_t offset, uintptr_t *addr);

static SceUID g_thread_uid = -1;
static bool g_thread_run = true;

static uint32_t *SceAppMgr_mutex_uid;
static uint32_t *SceAppMgr_app_list;

// If there's a better way of obtaining foreground app info, please do let me know
static int get_fg_app(char *out_titleid, char *out_title) {
    out_titleid[0] = '\0';
    out_title[0] = '\0';

    int ret = ksceKernelLockMutex(*SceAppMgr_mutex_uid, 1, 0);
    if (ret)
        return 0;

    uint32_t pcurrent;
    for (int i = 0; i < 20; i++) {
        pcurrent = (uint32_t)SceAppMgr_app_list + (i * 0x2410);

        SceUID pid = APP_LIST_GET_PID(pcurrent);
        if (pid <= 0)
            continue;

        uint32_t state = APP_LIST_GET_STATE(pcurrent);
        if (state != APP_RUNNING)
            continue;

        if (!ksceSblACMgrIsGameProgram(pid))
            continue;

        const char *titleid = (const char *)(APP_LIST_GET_TITLEID(pcurrent));
        if (strncmp(titleid, "PCS", 3))
            continue; // filter out homebrew

        snprintf(out_titleid, 10, "%s", titleid);
        snprintf(out_title, 128, "%s", (const char *)(APP_LIST_GET_TITLE(pcurrent)));
        ksceKernelUnlockMutex(*SceAppMgr_mutex_uid, 1);
        return i + 1;
    }

    ksceKernelUnlockMutex(*SceAppMgr_mutex_uid, 1);
    return 0;
}

static int vitapresence_thread(SceSize args, void *argp) {
    SceUID server_sockfd = -1;
    SceUID client_sockfd = -1;

    SceNetSockaddrIn clientaddr;
    SceNetSockaddrIn serveraddr;
    serveraddr.sin_family = SCE_NET_AF_INET;
    serveraddr.sin_addr.s_addr = SCE_NET_INADDR_ANY;
    serveraddr.sin_port = ksceNetHtons(VITAPRESENCE_PORT);

    unsigned int addrlen = sizeof(SceNetSockaddrIn);
    int ret = 0;

    while (g_thread_run) {
        server_sockfd = ksceNetSocket("vitapresence_socket", SCE_NET_AF_INET, SCE_NET_SOCK_STREAM, 0);
        if (server_sockfd < 0)
            return 0;

        do {
            ksceKernelDelayThread(2 * 1000 * 1000);

            ret = ksceNetBind(server_sockfd, (SceNetSockaddr *)&serveraddr, sizeof(SceNetSockaddrIn));
            if (ret < 0)
                continue;

            ret = ksceNetListen(server_sockfd, 128);
            if (ret < 0)
                continue;
        } while (ret < 0);

        while (g_thread_run && ret >= 0) {
            client_sockfd = ksceNetAccept(server_sockfd, (struct SceNetSockaddr*)&clientaddr, &addrlen);
            if (client_sockfd < 0)
                break;

            vitapresence_data_t presence_data;
            presence_data.magic = 0xCAFECAFE;
            presence_data.index = get_fg_app(presence_data.titleid, presence_data.title);

            ret = ksceNetSend(client_sockfd, &presence_data, sizeof(vitapresence_data_t), 0);
            ksceNetSocketClose(client_sockfd);
        }

        ksceNetSocketClose(server_sockfd);
    }

    return 0;
}

void _start() __attribute__ ((weak, alias ("module_start")));
int module_start(SceSize argc, const void *args) {

    tai_module_info_t tai_info;
    tai_info.size = sizeof(tai_module_info_t);
    if (taiGetModuleInfoForKernel(KERNEL_PID, "SceAppMgr", &tai_info) < 0)
        return SCE_KERNEL_START_SUCCESS;

    module_get_offset(KERNEL_PID, tai_info.modid, 1, 0x4A4, (uintptr_t *)&SceAppMgr_mutex_uid);
    module_get_offset(KERNEL_PID, tai_info.modid, 1, 0x500, (uintptr_t *)&SceAppMgr_app_list);

    g_thread_uid = ksceKernelCreateThread("vitapresence_thread", vitapresence_thread, 0x3C, 0x1000, 0, 0x10000, 0);
    if (g_thread_uid < 0)
        return SCE_KERNEL_START_SUCCESS;

    ksceKernelStartThread(g_thread_uid, 0, NULL);
    return SCE_KERNEL_START_SUCCESS;
}

int module_stop(SceSize argc, const void *args) {
    if (g_thread_uid >= 0) {
        g_thread_run = false;
        ksceKernelWaitThreadEnd(g_thread_uid, NULL, NULL);
        ksceKernelDeleteThread(g_thread_uid);
    }

    return SCE_KERNEL_STOP_SUCCESS;
}
