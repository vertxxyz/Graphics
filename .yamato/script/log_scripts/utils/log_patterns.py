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
        'tags': ['tests'],
        'conclusion': 'failure',
    },
    {
        'pattern': r'Reason\(s\): One or more non-test related errors or failures occurred.', # if hit this, read hoarder file
        'tags': ['non-test'], #todo proper tags
        'conclusion': 'failure',
    },
    {
        'pattern': r'(command not found)',
        'tags': ['failure'],
        'conclusion': 'failure',
    }
]
