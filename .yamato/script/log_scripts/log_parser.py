
import argparse
import os
import sys
import requests
import json
import glob
import re
from utils.execution_log_patterns import execution_log_patterns
from utils.hoarder_log_patterns import hoarder_log_patterns

def read_hoarder_log(log_file_path):
    '''Reads error message from hoarder data, when UTR results in non-test related error which is not in the execution log.'''
    with open(log_file_path) as f:
        logs = json.load(f)

    failure_reasons = ' '.join(logs.get('suites',[{}])[0].get('failureReasons'))
    for pattern in hoarder_log_patterns:
            match = re.search(pattern['pattern'], failure_reasons)

            if match:
                print('\nFound hoarder failure match for: ',  pattern['pattern'])
                return failure_reasons, pattern['tags'], pattern['conclusion']

def read_execution_log(log_file_path):
    '''Reads execution logs and returns:
    logs: dictionary with keys corresponding to commands, and values containing log output and status
    overall_status: success/failure status for the whole job
    '''

    with open(log_file_path) as f:
        lines = [l.replace('\n','') for l in f.readlines() if l != '\n'] # remove empty lines and all newline indicators

    # all log line idx starting/ending a new command
    command_idxs = [i for i,line  in enumerate(lines) if '################################### Running next command ###################################' in line]
    command_idxs_end = [i for i,line  in enumerate(lines) if '############################################################################################' in line]
    command_idxs.append(len(lines)) # add dummy idx to handle the last command

    # get output (list of lines) for each command
    logs = {}
    for i, command_idx in enumerate(command_idxs):
        if command_idx == len(lines):
            break
        command = '\n'.join(lines[command_idx+1: command_idxs_end[i]])
        output = lines[command_idx+3: command_idxs[i+1]-1]
        logs[command] = {}
        logs[command]['output'] = output
        logs[command]['status'] = 'Failed' if any("Command failed" in line for line in output) else 'Success'

    # if the command block succeeded overall
    overall_status = [line for line in lines if 'Commands finished with result:' in line][0].split(']')[1].split(': ')[1]

    return logs, overall_status


def post_additional_results(cmd, local):

    data = {
        'title': cmd['title'],
        'summary': cmd['summary'],
        'conclusion': cmd['conclusion'],
        'tags' : cmd['tags']
    }

    print('\nPosting: ', json.dumps(data,indent=2))
    if not local:
        server_url = os.environ['YAMATO_REPORTING_SERVER'] + '/result'
        headers = {'Content-Type':'application/json'}
        res = requests.post(server_url, json=data, headers=headers)
        if res.status_code != 200:
                raise Exception(f'!! Error: Got {res.status_code}')

def parse_failures(logs, local):
    for cmd in logs.keys():

        # skip parsing successful commands, or failed tests (these get automatically parsed in yamato results)
        # TODO: do we also want to add additional yamato results for these?
        if logs[cmd]['status'] == 'Success' or any("Reason(s): One or more tests have failed." in line for line in logs[cmd]['output']):
            print('Skipping: ', cmd)
            continue

        # check if the error matches any known pattern marked in log_patterns.py
        output = '\n'.join(logs[cmd]['output'])
        for pattern in execution_log_patterns:
            match = re.search(pattern['pattern'], output)

            if match:
                print('\nFound execution log failure match for: ', cmd, '\nFor pattern: ', pattern['pattern'])
                logs[cmd]['title'] = cmd
                logs[cmd]['summary'] = match.group(0) if pattern['tags'][0] != 'unknown' else 'Unknown failure: check logs for more details.'
                logs[cmd]['conclusion'] = pattern['conclusion']
                logs[cmd]['tags'] = pattern['tags']

                # if it is an UTR non-test related error message not shown in Execution log but in test-results, append that to summary
                if logs[cmd]['tags'][0] == 'non-test':
                    test_results_match = re.findall(r'(--artifacts_path=)(.+)(test-results)', cmd)[0]
                    test_results_path = test_results_match[1] + test_results_match[2]
                    hoarder_failures, hoarder_tags, hoarder_conclusion = read_hoarder_log(os.path.join(test_results_path,'HoarderData.json'))
                    logs[cmd]['summary'] += hoarder_failures
                    logs[cmd]['tags'].extend(hoarder_tags)
                    logs[cmd]['conclusion'] = hoarder_conclusion

                # post additional results to Yamato
                post_additional_results(logs[cmd], local)
                return



def get_execution_log():
    '''Returns the path to execution log file.'''
    path_to_execution_log = os.path.join(os.path.dirname(os.path.dirname(os.getcwd())),'Execution-*.log')
    print('Searching for logs in: ', path_to_execution_log)

    execution_log_file = glob.glob(path_to_execution_log)[0]
    print('Reading log: ', execution_log_file)
    return execution_log_file


def parse_args(argv):
    parser = argparse.ArgumentParser()
    parser.add_argument("--execution-log", required=False, help='Path to execution log file. If not specified, ../../Execution-*.log is used.', default=None)
    parser.add_argument("--local", action='store_true', help='If specified, API call to post additional results is skipped.', default=False)
    args = parser.parse_args(argv)
    return args


def main(argv):
    args = parse_args(argv)

    # read execution logs
    execution_log_file = get_execution_log() if not args.execution_log else args.execution_log
    logs, overall_status = read_execution_log(execution_log_file)

    # only parse failures if the job has failed
    if 'Failed' in overall_status:
        parse_failures(logs, args.local)

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
