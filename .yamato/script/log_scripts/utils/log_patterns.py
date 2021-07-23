# conclusion can be either: success, failure, cancelled, inconclusive

log_patterns = [
    {
        # 'pattern': r'(packet_write_poll: Connection to)((.|\n)+)(Operation not permitted)((.|\n)+)(lost connection)',
        'pattern': r'(packet_write_poll: Connection to)(.+)(Operation not permitted)',
        'tags': ['instability'],
        'conclusion': 'inconclusive',
    },
    {
        'pattern': r'Reason\(s\): One or more tests have failed.', # this one is unused right now since yamato does it automatically
        'tags': ['tests failed'],
        'conclusion': 'failure',
    },
    {
        'pattern': r'(command not found)',
        'tags': ['failure'],
        'conclusion': 'failure',
    }
]
