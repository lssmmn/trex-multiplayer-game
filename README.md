# T-Rex 런너 멀티플레이어 대전 게임

## 프로젝트 개요
기존 단일 플레이어 T-Rex 런너 게임을 실시간 멀티플레이어 대전 게임으로 확장한 프로젝트입니다. TCP/IP 소켓 통신과 MySQL을 활용하여 2명의 플레이어가 동시에 같은 장애물 맵에서 경쟁합니다.

## 프로젝트 정보
- **개발자**: 이수민
- **개발 기간**: 2025.10.13 ~ 2025.10.17
- **개발 장소**: 드론융합실

## 개발 환경
- **OS**: Windows 10
- **언어**: C# (.NET Framework 4.7.2)
- **IDE**: Visual Studio 2022
- **네트워크**: System.Net.Sockets
- **데이터베이스**: MySQL 8.0

## 주요 구현 기능

### 1. 네트워킹 시스템
- TCP/IP 소켓 통신 (TcpListener, TcpClient)
- 멀티스레딩 (네트워크 수신 스레드와 게임 로직 분리)
- 커스텀 메시지 프로토콜 (직렬화/역직렬화)

### 2. 매칭 시스템
- 닉네임 설정 및 유효성 검증 (2-10자)
- 자동 대기열 관리 (2명 매칭)
- 재매칭 기능 (R키)

### 3. 실시간 멀티플레이어 게임
- 동기화된 장애물 맵
- 실시간 위치 및 점수 동기화
- 승패 판정 시스템

### 4. 데이터베이스 시스템
- 플레이어 정보 및 게임 기록 저장
- 통계 시스템 (승률, 최고 점수)
- 리더보드 (상위 10명)

### 5. 사용자 인터페이스
- 연결 상태 및 매칭 대기 상태 표시
- 상대방 위치 실시간 표시
- 게임 결과 중앙 메시지

## 개발 과정에서 겪은 문제와 해결

### 1. MySQL 데이터베이스 연결 문제
**문제**: NuGet 패키지 설치 및 연결 방법 미숙지

**해결**: MySql.Data 패키지 설치 후 connectionString 형식 수정

### 2. TCP/IP 통신 불안정
**문제**: 간헐적 연결 실패 및 메시지 미전달

**해결**: NetworkStream.Flush() 추가, Thread.Sleep()으로 안정화, 재연결 로직 구현

### 3. 메시지 역직렬화 실패
**문제**: Deserialize 시 null 반환

**해결**: 메시지 형식을 Type|Data로 단순화, UTF-8 인코딩 명시

### 4. 멀티스레딩 동기화 문제
**문제**: UI 스레드 외부에서 컨트롤 접근 시 예외 발생

**해결**: Invoke/MethodInvoker 사용, lock 키워드로 동기화

## 데이터베이스 스키마

```sql
-- 플레이어 테이블
CREATE TABLE players (
    player_id INT AUTO_INCREMENT PRIMARY KEY,
    player_name VARCHAR(50) UNIQUE NOT NULL,
    total_games INT DEFAULT 0,
    total_wins INT DEFAULT 0,
    total_losses INT DEFAULT 0,
    highest_score INT DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 게임 기록 테이블
CREATE TABLE game_records (
    game_id INT AUTO_INCREMENT PRIMARY KEY,
    player1_id INT NOT NULL,
    player2_id INT NOT NULL,
    player1_score INT NOT NULL,
    player2_score INT NOT NULL,
    winner_id INT NOT NULL,
    game_duration INT,
    played_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## 설치 및 실행

1. Visual Studio 2022에서 솔루션 열기
2. NuGet에서 `MySql.Data` 패키지 설치
3. MySQL 서버 실행 및 데이터베이스 생성
4. 서버 프로젝트 실행 후 클라이언트 2개 실행

## 프로젝트 구조
```
T-Rex-Multiplayer/
├── TRexClient/          # 클라이언트
├── TRexServer/          # 서버 및 DB 관리
└── TRexShared/          # 공유 메시지 클래스
```

## 개발 성과
- TCP/IP 소켓 통신 및 클라이언트-서버 아키텍처 구현
- 멀티스레딩 및 UI 스레드 안전성 확보
- 실시간 게임 상태 동기화 메커니즘 이해
- MySQL과 C# 연동 경험

## 라이센스
이 프로젝트는 교육 목적으로 제작되었습니다.
