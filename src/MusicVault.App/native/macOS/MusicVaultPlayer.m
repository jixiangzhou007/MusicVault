// MusicVault macOS 音频播放原生助手
// 编译: clang -dynamiclib -framework Foundation -framework AVFoundation
//       -o libMusicVaultPlayer.dylib MusicVaultPlayer.m
//       -install_name @executable_path/libMusicVaultPlayer.dylib

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

// 返回 void* 避免头文件依赖；C# 侧用 IntPtr
typedef void* MVPlayerHandle;

// ── 生命周期 ──

MVPlayerHandle MusicVaultPlayer_Create(const char* filePath) {
    @autoreleasepool {
        NSString *path = [NSString stringWithUTF8String:filePath];
        if (!path || ![[NSFileManager defaultManager] fileExistsAtPath:path]) {
            NSLog(@"[MusicVault] 文件不存在: %s", filePath ?: "(null)");
            return NULL;
        }
        NSURL *url = [NSURL fileURLWithPath:path];
        NSError *error = nil;
        AVAudioPlayer *player = [[AVAudioPlayer alloc] initWithContentsOfURL:url error:&error];
        if (error || !player) {
            NSLog(@"[MusicVault] AVAudioPlayer 初始化失败: %@", error.localizedDescription);
            return NULL;
        }
        [player prepareToPlay];
        // 返回桥接指针（C# 侧通过 CFBridgingRetain/Release 或 GCHandle 管理）
        return (__bridge_retained void*)player;
    }
}

void MusicVaultPlayer_Destroy(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return;
        AVAudioPlayer *player = (__bridge_transfer AVAudioPlayer*)handle;
        [player stop];
        player = nil;
    }
}

// ── 播放控制 ──

int MusicVaultPlayer_Play(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return 0;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        return [player play] ? 1 : 0;
    }
}

void MusicVaultPlayer_Pause(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        [player pause];
    }
}

void MusicVaultPlayer_Stop(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        [player stop];
        player.currentTime = 0;
    }
}

// ── 状态查询 ──

int MusicVaultPlayer_IsPlaying(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return 0;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        return player.isPlaying ? 1 : 0;
    }
}

double MusicVaultPlayer_GetCurrentTime(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return 0.0;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        return player.currentTime;
    }
}

double MusicVaultPlayer_GetDuration(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return 0.0;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        return player.duration;
    }
}

void MusicVaultPlayer_SetCurrentTime(MVPlayerHandle handle, double seconds) {
    @autoreleasepool {
        if (!handle) return;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        player.currentTime = seconds;
    }
}

void MusicVaultPlayer_SetVolume(MVPlayerHandle handle, float volume) {
    @autoreleasepool {
        if (!handle) return;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        player.volume = volume;
    }
}

float MusicVaultPlayer_GetVolume(MVPlayerHandle handle) {
    @autoreleasepool {
        if (!handle) return 0.8f;
        AVAudioPlayer *player = (__bridge AVAudioPlayer*)handle;
        return player.volume;
    }
}
